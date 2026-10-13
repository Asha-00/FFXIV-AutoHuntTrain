using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Conditions;
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
internal sealed class RideLeg(FlagPost flag, string label, bool alreadyBroughtIn) : AutoCommon
{
    // A flag marks where the conductor stood, and the mark is pulled near it; getting closer only costs time.
    public const float ArriveWithinMeters = 15f;

    private const int TeleportWatchdogMs = 60_000;
    private const int NavmeshWaitMs = 60_000;
    private const int TeleportWaitPollFrames = 10;
    // A teleport already casting or loading when the wait runs out is given this long to land.
    private const int TeleportUnderwayGraceMs = 30_000;
    // A wait set to never end still does after as long as a leg may travel: the train has moved on by then.
    private const int UnlimitedTeleportWaitMs = AutoRide.LegBudgetMs;

    // Set when someone else's teleport brought the character in, so the leg does not hop to another aetheryte of the zone.
    private bool broughtIn = alreadyBroughtIn;

    private enum TeleportWaitOutcome : byte { BroughtIn, TeleportItself, GiveUp }

    public RideLegOutcome Outcome { get; private set; } = RideLegOutcome.Cancelled;

    // Only while it waits for someone else's teleport: before the wait and after it the leg makes its own way.
    public bool AwaitsTeleport { get; private set; }

    // The task runner swallows exceptions, so a leg that blew up would otherwise look like a quiet failure.
    public Exception? Fault { get; private set; }

    // How much longer than its own budget a leg to this flag may run, since the wait for someone else's teleport is
    // not travel time.
    public static int TeleportWaitAllowanceMs(in FlagPost post)
    {
        var configuration = Plugin.Instance.Configuration;
        if (configuration.FlagTeleportMode != FlagTeleportMode.WaitForTeleport || Svc.ClientState.TerritoryType == post.TerritoryId)
        {
            return 0;
        }

        return TeleportWaitLimitMs(configuration) + TeleportUnderwayGraceMs;
    }

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
        return await TravelTo(territoryId, destination, ArriveWithinMeters, teleportShortcut: !broughtIn) ? RideLegOutcome.Arrived : Unreached(RideLegOutcome.FlagUnreached);
    }

    private async Task<bool> EnterZone(uint territoryId, Vector3 flat, string zoneName)
    {
        if (Plugin.Instance.Configuration.FlagTeleportMode == FlagTeleportMode.WaitForTeleport)
        {
            AwaitsTeleport = true;
            var waited = await WaitToBeBroughtIn(territoryId, zoneName);
            AwaitsTeleport = false;
            if (waited != TeleportWaitOutcome.TeleportItself)
            {
                return waited == TeleportWaitOutcome.BroughtIn;
            }
        }

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

    // The player, a party member's teleport or another plugin brings the character in, and the ride casts its own
    // teleport only once the wait runs out; a wait set to never end gives the leg up instead. Landing at the gateway of
    // a zone owning no aetheryte also ends it, and the aethernet ride in is then all that is left.
    private async Task<TeleportWaitOutcome> WaitToBeBroughtIn(uint territoryId, string zoneName)
    {
        var configuration = Plugin.Instance.Configuration;
        var teleportsAfter = TeleportWaitSeconds(configuration) > 0;
        var limitMs = TeleportWaitLimitMs(configuration);
        var limitSeconds = limitMs / TimeUnits.MillisecondsPerSecond;
        var startedAt = Environment.TickCount64;
        var deadline = startedAt + limitMs;
        var gatewayTerritoryId = ZoneAetherytes.TryFindGateway(territoryId, out var gateway) ? gateway.TerritoryId : 0u;
        Status = Loc.T(L.Ride.WaitingForTeleport, zoneName);
        Diag(teleportsAfter
            ? $"{label}: in territory {Svc.ClientState.TerritoryType}, waiting up to {limitSeconds}s for a teleport into {zoneName} before teleporting itself"
            : $"{label}: in territory {Svc.ClientState.TerritoryType}, waiting up to {limitSeconds}s for a teleport into {zoneName}; the ride does not teleport itself");
        while (!CancelToken.IsCancellationRequested)
        {
            var here = Svc.ClientState.TerritoryType;
            if (here == territoryId)
            {
                broughtIn = true;
                Diag($"{label}: brought into {zoneName} {SecondsSince(startedAt)}s into the wait ({ConditionTag()}); no teleport within the zone on the way to the flag");
                return TeleportWaitOutcome.BroughtIn;
            }

            if (gatewayTerritoryId != 0 && here == gatewayTerritoryId)
            {
                broughtIn = true;
                Diag($"{label}: brought to {gateway.Name}, the way into {zoneName}, {SecondsSince(startedAt)}s into the wait; riding its aethernet in from there");
                return TeleportWaitOutcome.TeleportItself;
            }

            var now = Environment.TickCount64;
            if (now >= deadline && (!TeleportUnderway() || now >= deadline + TeleportUnderwayGraceMs))
            {
                if (!teleportsAfter)
                {
                    Warn($"{label}: nobody brought the character into {zoneName} within {limitSeconds}s (now in territory {here}, {ConditionTag()}); the leg is given up");
                    return TeleportWaitOutcome.GiveUp;
                }

                Diag($"{label}: no teleport into {zoneName} within {limitSeconds}s (now in territory {here}, {ConditionTag()}); teleporting itself");
                return TeleportWaitOutcome.TeleportItself;
            }

            await NextFrame(TeleportWaitPollFrames);
        }

        Diag($"{label}: the wait for a teleport into {zoneName} was cut after {SecondsSince(startedAt)}s");
        return TeleportWaitOutcome.GiveUp;
    }

    private static int TeleportWaitSeconds(Configuration configuration)
        => Math.Clamp(configuration.FlagTeleportWaitSeconds, 0, Configuration.FlagTeleportWaitSecondsMax);

    private static int TeleportWaitLimitMs(Configuration configuration)
    {
        var seconds = TeleportWaitSeconds(configuration);
        return seconds == 0 ? UnlimitedTeleportWaitMs : seconds * TimeUnits.MillisecondsPerSecond;
    }

    private static bool TeleportUnderway()
        => Svc.Condition[ConditionFlag.Casting]
        || Svc.Condition[ConditionFlag.BetweenAreas]
        || Svc.Condition[ConditionFlag.BetweenAreas51];

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
