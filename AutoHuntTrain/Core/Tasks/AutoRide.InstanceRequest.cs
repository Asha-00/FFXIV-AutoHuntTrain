using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// A conductor's line such as "swap to 2" calls an instance of the zone the train is in. For a while the call sends every
// flag of that zone to the instance called, unless the flag's own text names one, and while the ride waits with no
// flag it switches there itself. A newer flag naming its instance in the text, or a flag for another zone, supersedes
// the call, and a fight is never cut short for it.
internal sealed partial class AutoRide
{
    // A flag for the next zone often follows a call within seconds, and then the call was for that zone.
    private const int InstanceRequestGraceMs = 5_000;
    private const int InstanceRequestLifetimeMs = 2 * TimeUnits.MillisecondsPerMinute;
    // Room for a teleport to an aetheryte and every attempt of the switch.
    private const int InstanceSwitchBudgetMs = 3 * TimeUnits.MillisecondsPerMinute;
    private const string InstanceCallScope = "Instance call";

    private InstanceRequest instanceRequest;
    private InstanceRequest activeSwitch;
    private bool hasInstanceRequest;
    // Once a flag, a leg or the ride itself has acted on the call, the wait between flags no longer switches for it.
    private bool instanceRequestHandled;
    private int instanceSwitchCount;

    private void QueueInstanceRequest(int instance, in SpokenLine line)
    {
        var territoryId = InstanceRequestTerritory();
        var zoneName = TerritoryNames.Of(territoryId);
        var now = Environment.TickCount64;
        if (hasInstanceRequest
            && instanceRequest.TerritoryId == territoryId
            && instanceRequest.Instance == instance
            && now - instanceRequest.HeardAtMs < InstanceRequestLifetimeMs)
        {
            RunLog.Debug($"{InstanceCallScope}: \"{line.Text}\" from {Conductor.Describe(conductor)} via {line.ChatType} repeats the call for instance {instance} of {zoneName}; ignored");
            return;
        }

        var heard = $"{InstanceCallScope}: {Conductor.Describe(conductor)} said \"{line.Text}\" via {line.ChatType}, calling instance {instance} of {zoneName}";
        if (territoryId == Svc.ClientState.TerritoryType && LifestreamIPC.Instance.NumberOfInstances() == 0)
        {
            Diag($"{heard}, which has no instances; ignored");
            return;
        }

        instanceRequest = new InstanceRequest(territoryId, instance, now);
        hasInstanceRequest = true;
        instanceRequestHandled = false;
        quietSinceMs = now;
        if (hasPending)
        {
            instanceRequestHandled = true;
            pending = pending with { Instance = instance, InstanceFromLink = false };
            Diag($"{heard}; the flag waiting to be followed goes there");
            return;
        }

        if (!hasLegFlag)
        {
            Diag(engaging
                ? $"{heard}; the switch waits until the fight in progress finishes"
                : $"{heard}; switching in {InstanceRequestGraceMs / TimeUnits.MillisecondsPerSecond}s unless a flag comes first");
            return;
        }

        instanceRequestHandled = true;
        if (legFlag.Instance == instance)
        {
            Diag($"{heard}; the leg in progress already goes there");
            return;
        }

        pending = legFlag with { Instance = instance, InstanceFromLink = false };
        hasPending = true;
        Diag($"{heard}; the leg to {zoneName} starts over for that instance");
    }

    // A call is for the zone of the flag waiting or being travelled to, else for the zone the ride is in.
    private uint InstanceRequestTerritory()
    {
        if (hasPending)
        {
            return pending.TerritoryId;
        }

        return hasLegFlag ? legFlag.TerritoryId : Svc.ClientState.TerritoryType;
    }

    // A flag of the zone called whose text names no instance goes to the one called; the link only says where the
    // conductor stood when posting it.
    private FlagPost WithRequestedInstance(in FlagPost post)
    {
        if (!InstanceRequestLive()
            || post.TerritoryId != instanceRequest.TerritoryId
            || post.NamesInstanceInText
            || post.Instance == instanceRequest.Instance)
        {
            return post;
        }

        return post with { Instance = instanceRequest.Instance, InstanceFromLink = false };
    }

    // A newer flag wins: one for another zone means the train moved on, and one naming another instance in its text
    // says where to go. Any other flag of the zone called takes the call along.
    private void NoteFlagAgainstInstanceRequest(in FlagPost heard, in FlagPost taken)
    {
        if (!hasInstanceRequest)
        {
            return;
        }

        if (heard.TerritoryId != instanceRequest.TerritoryId)
        {
            ClearInstanceRequest($"a flag for {TerritoryNames.Of(heard.TerritoryId)} came after it");
            return;
        }

        if (taken.Instance != instanceRequest.Instance)
        {
            ClearInstanceRequest($"a newer flag names instance {heard.Instance} in its text");
            return;
        }

        instanceRequestHandled = true;
        if (heard.Instance != taken.Instance)
        {
            Diag($"{InstanceCallScope}: the flag in {TerritoryNames.Of(heard.TerritoryId)} goes to instance {taken.Instance} as called{(heard.NamesInstance ? $", not to instance {heard.Instance} from its map link" : string.Empty)}");
        }
    }

    // Due once the grace has passed with no flag, in the zone called, and never during a fight. Polled every few frames,
    // so it only reads fields.
    private bool InstanceInterruptNeeded()
    {
        if (!hasInstanceRequest || instanceRequestHandled || engaging)
        {
            return false;
        }

        var ageMs = Environment.TickCount64 - instanceRequest.HeardAtMs;
        return ageMs >= InstanceRequestGraceMs
            && ageMs < InstanceRequestLifetimeMs
            && instanceRequest.TerritoryId == Svc.ClientState.TerritoryType;
    }

    // Waiting at a flag with no mark in view, a call for another instance means the mark stands there, so the flag is
    // followed again in that instance.
    private bool RefollowForInstance(in FlagPost flag, string label, string zoneName)
    {
        instanceRequestHandled = true;
        var instance = instanceRequest.Instance;
        var current = LifestreamIPC.Instance.CurrentInstance();
        if (current == instance)
        {
            Diag($"{label}: the conductor called instance {instance}, the one the ride is in; waiting on at the flag in {zoneName}");
            return false;
        }

        pending = flag with { Instance = instance, InstanceFromLink = false };
        hasPending = true;
        Diag($"{label}: the conductor called instance {instance} and no mark is in view at the flag in {zoneName} instance {current}; following the flag again there");
        return true;
    }

    // While the ride waits with no flag, it switches to the instance called itself. True when it tried, so the
    // caller's loop starts over.
    private async Task<bool> HandlePendingInstanceRequest()
    {
        if (!InstanceRequestLive() || instanceRequestHandled)
        {
            return false;
        }

        var request = instanceRequest;
        var territoryId = Svc.ClientState.TerritoryType;
        if (request.TerritoryId != territoryId)
        {
            ClearInstanceRequest($"the ride is in {TerritoryNames.Of(territoryId)} now");
            return false;
        }

        if (!InstanceInterruptNeeded())
        {
            return false;
        }

        instanceRequestHandled = true;
        var zoneName = TerritoryNames.Of(territoryId);
        var lifestream = LifestreamIPC.Instance;
        if (lifestream.NumberOfInstances() == 0)
        {
            ClearInstanceRequest($"{zoneName} has no instances");
            return false;
        }

        var current = lifestream.CurrentInstance();
        if (current == request.Instance)
        {
            Diag($"{InstanceCallScope}: already in instance {request.Instance} of {zoneName}; nothing to switch");
            return false;
        }

        await HoldWhileTyping("the instance switch");
        if (CancelToken.IsCancellationRequested || hasPending || conductorEnded)
        {
            return true;
        }

        var label = $"instance-call#{++instanceSwitchCount}";
        progress.SetRidePhase(RidePhase.Travelling);
        Diag($"{label}: no flag came {InstanceRequestGraceMs / TimeUnits.MillisecondsPerSecond}s after the call; switching {zoneName} from instance {current} to {request.Instance} ({ConditionTag()})");
        var switcher = new InstanceSwitch(request.Instance, label);
        var completed = false;
        activeSwitch = request;
        await RunWithStatusFrom(switcher, async () => completed = await RunCancellable(switcher, InstanceSwitchBudgetMs, label, InstanceSwitchSuperseded));
        if (!CancelToken.IsCancellationRequested)
        {
            NoteInstanceSwitch(switcher, completed, request, label, zoneName);
        }

        BeginWaiting();
        return true;
    }

    // A flag for the zone and instance being switched to lets the switch finish, since its leg needs the same switch.
    private bool InstanceSwitchSuperseded()
        => conductorEnded
        || InstanceInterruptNeeded()
        || (hasPending && (pending.TerritoryId != activeSwitch.TerritoryId || pending.Instance != activeSwitch.Instance));

    private void NoteInstanceSwitch(InstanceSwitch switcher, bool completed, in InstanceRequest request, string label, string zoneName)
    {
        if (switcher.Fault is { } fault)
        {
            RunLog.Warning(fault, $"{label}: the switch to instance {request.Instance} of {zoneName} ended with an unexpected error");
            DropInstanceRequest(request, "the switch hit an error");
            return;
        }

        if (!completed)
        {
            NoteSwitchCut(request, label, zoneName);
            return;
        }

        if (switcher.Switched)
        {
            Diag($"{label}: in instance {LifestreamIPC.Instance.CurrentInstance()} of {zoneName} as the conductor called; waiting for the next flag");
            return;
        }

        Warn($"{label}: could not switch {zoneName} to instance {request.Instance}; waiting for the next flag where the ride is");
        DropInstanceRequest(request, "the switch failed");
    }

    private void NoteSwitchCut(in InstanceRequest request, string label, string zoneName)
    {
        var busy = StopLeftoverLifestream();
        if (InstanceSwitchSuperseded())
        {
            Diag($"{label}: a newer flag or call, or the end of the train, made the switch to instance {request.Instance} of {zoneName} moot; cut short{(busy ? " and Lifestream stopped" : string.Empty)}");
            return;
        }

        Warn($"{label}: the switch to instance {request.Instance} of {zoneName} did not finish within {InstanceSwitchBudgetMs / TimeUnits.MillisecondsPerSecond}s{(busy ? "; Lifestream stopped" : string.Empty)}");
        DropInstanceRequest(request, "the switch ran out of time");
    }

    // Lifestream carries on with a switch on its own, so one a switch or a leg was cut short in is stopped there too, or
    // it would land the character in an instance nobody wants any more, mid-way through the next leg. True when it was busy.
    private static bool StopLeftoverLifestream()
    {
        var lifestream = LifestreamIPC.Instance;
        if (!lifestream.IsBusy())
        {
            return false;
        }

        lifestream.Abort();
        return true;
    }

    // A switch a leg could not make is not tried again for every flag of the call's lifetime.
    private void DropInstanceRequestAfterLeg(RideLeg leg, in FlagPost flag)
    {
        if (leg.Outcome == RideLegOutcome.InstanceUnreached
            && hasInstanceRequest
            && instanceRequest.TerritoryId == flag.TerritoryId
            && instanceRequest.Instance == flag.Instance)
        {
            ClearInstanceRequest("the leg could not switch there");
        }
    }

    // Only the call that was acted on is dropped; a newer one heard meanwhile stays.
    private void DropInstanceRequest(in InstanceRequest request, string reason)
    {
        if (hasInstanceRequest && instanceRequest == request)
        {
            ClearInstanceRequest(reason);
        }
    }

    private bool InstanceRequestLive()
    {
        if (!hasInstanceRequest)
        {
            return false;
        }

        if (Environment.TickCount64 - instanceRequest.HeardAtMs < InstanceRequestLifetimeMs)
        {
            return true;
        }

        ClearInstanceRequest($"{InstanceRequestLifetimeMs / TimeUnits.MillisecondsPerSecond}s after it was heard");
        return false;
    }

    private void ClearInstanceRequest(string reason)
    {
        Diag($"{InstanceCallScope}: the call for instance {instanceRequest.Instance} of {TerritoryNames.Of(instanceRequest.TerritoryId)} is dropped, {reason}");
        hasInstanceRequest = false;
        instanceRequestHandled = false;
    }

    private readonly record struct InstanceRequest(uint TerritoryId, int Instance, long HeardAtMs);
}
