using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

internal enum RideLegOutcome : byte
{
    Arrived,
    ZoneUnreached,
    InstanceUnreached,
    FlagUnreached,
    Cancelled,
    Faulted,
}

// One leg of a ride, run as its own task so the ride can cancel it the moment a newer flag arrives, with every
// teleport and move inside it unwinding through the leg's own cancellation.
internal sealed class RideLeg(FlagPost flag, string label) : AutoCommon
{
    // A flag marks where the conductor stood, and the mark is pulled near it; getting closer only costs time.
    public const float ArriveWithinMeters = 15f;

    private const int TeleportWatchdogMs = 60_000;
    private const int NavmeshWaitMs = 60_000;

    public RideLegOutcome Outcome { get; private set; } = RideLegOutcome.Cancelled;

    // The task runner swallows exceptions, so a leg that blew up would otherwise look like a quiet failure.
    public Exception? Fault { get; private set; }

    protected override async Task Execute()
    {
        try
        {
            Outcome = await Follow();
        }
        catch (OperationCanceledException)
        {
            Outcome = RideLegOutcome.Cancelled;
        }
        catch (Exception exception)
        {
            Fault = exception;
            Outcome = RideLegOutcome.Faulted;
        }
    }

    private async Task<RideLegOutcome> Follow()
    {
        var territoryId = flag.TerritoryId;
        var zoneName = TerritoryNames.Of(territoryId);
        var flat = new Vector3(flag.WorldX, float.NaN, flag.WorldZ);
        if (Svc.ClientState.TerritoryType != territoryId)
        {
            if (!await EnterZone(territoryId, flat, zoneName))
            {
                return Unreached(RideLegOutcome.ZoneUnreached);
            }
        }
        else if (NeedsInstanceChange() && !LifestreamIPC.Instance.CanChangeInstance())
        {
            await TeleportToNearestAetheryte(territoryId, flat, zoneName);
        }

        if (flag.NamesInstance && !await SwitchToInstance(flag.Instance, label))
        {
            return Unreached(RideLegOutcome.InstanceUnreached);
        }

        await WaitForNavmeshReady(NavmeshWaitMs);
        if (CancelToken.IsCancellationRequested)
        {
            return RideLegOutcome.Cancelled;
        }

        var destination = SnapMarkHeight(flat) ?? flat with { Y = Svc.Objects.LocalPlayer?.Position.Y ?? 0f };
        return await TravelTo(territoryId, destination, ArriveWithinMeters) ? RideLegOutcome.Arrived : Unreached(RideLegOutcome.FlagUnreached);
    }

    private async Task<bool> EnterZone(uint territoryId, Vector3 flat, string zoneName)
    {
        Diag($"{label}: in territory {Svc.ClientState.TerritoryType}, teleporting toward the flag in {zoneName}");
        var reached = false;
        await RunWithStatusPinned(
            $"Teleporting to {zoneName}",
            async () =>
            {
                if (await HumanDelay(HumanAction.Teleport, label))
                {
                    reached = await TeleportToTerritory(territoryId, flat, $"{label}-teleport", TeleportWatchdogMs);
                }
            });
        if (reached || CancelToken.IsCancellationRequested)
        {
            return reached;
        }

        Warn($"{label}: could not reach {zoneName} (still in territory {Svc.ClientState.TerritoryType})");
        return false;
    }

    private bool NeedsInstanceChange()
        => flag.NamesInstance && LifestreamIPC.Instance.CurrentInstance() != flag.Instance;

    // The instance is switched at an aetheryte, and the one nearest the flag is also the best place to start from.
    private async Task TeleportToNearestAetheryte(uint territoryId, Vector3 flat, string zoneName)
    {
        if (!ZoneAetherytes.TryFindNearest(territoryId, flat with { Y = 0f }, out var aetheryte))
        {
            Diag($"{label}: no attuned aetheryte in {zoneName} to switch instances at; walking to the nearest one instead");
            return;
        }

        Diag($"{label}: instance {flag.Instance} named while in instance {LifestreamIPC.Instance.CurrentInstance()}; teleporting to {aetheryte.Name} to switch");
        if (!await HumanDelay(HumanAction.Teleport, label))
        {
            return;
        }

        await TeleportToAetheryte(territoryId, aetheryte, $"{label}-aetheryte");
    }

    private RideLegOutcome Unreached(RideLegOutcome outcome)
        => CancelToken.IsCancellationRequested ? RideLegOutcome.Cancelled : outcome;
}
