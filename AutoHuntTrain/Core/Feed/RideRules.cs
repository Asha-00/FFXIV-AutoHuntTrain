using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Feed;

internal enum RideVerdict : byte
{
    Rideable,
    FeedWorldUnknown,
    RideRunning,
    OutOfRegion,
    InDuty,
    LifestreamBusy,
    Snoozed,
    AutoJoinOff,
    NotAllowedDataCenter,
    CrossDataCenterOff,
    SRankCrossDataCenter,
    TooSoon,
    TooLate,
}

internal enum Reachability : byte
{
    SameWorld,
    SameDataCenter,
    CrossDataCenter,
    OutOfRegion,
}

// Whether an announcement can be acted on. A manual Ride or S-Hunt is refused only by what makes it impossible
// right now; S-Hunts additionally stay within the current data center. Train auto-join also needs the player's rules:
// the expansion switch, snooze, allowed data centers, cross-data-center setting and clock.
internal static class RideRules
{
    public static RideVerdict EvaluateManual(in Announcement announcement, out Reachability reachability)
    {
        var placement = EvaluatePlacement(announcement, ignoreRunningRide: false, out reachability);
        if (placement != RideVerdict.Rideable)
        {
            return placement;
        }

        // World Visit within the current data center is supported. Data-center travel is not: S ranks are short-lived,
        // and a transfer logs the character out, losing the active one-off run unless it receives separate persistence.
        if (announcement.IsSRank && reachability == Reachability.CrossDataCenter)
        {
            return RideVerdict.SRankCrossDataCenter;
        }

        return EvaluateCharacter();
    }

    public static RideVerdict EvaluateAuto(in Announcement announcement, DateTime nowUtc, out Reachability reachability)
        => EvaluateAuto(announcement, nowUtc, ignoreRunningRide: false, out reachability);

    // Asked by a ride that has just ended, while it may still hold the controller, about the train auto-join would
    // take next; that ride is not a reason to refuse.
    public static RideVerdict EvaluateNextTrain(in Announcement announcement, DateTime nowUtc, out Reachability reachability)
        => EvaluateAuto(announcement, nowUtc, ignoreRunningRide: true, out reachability);

    // The player's rules come before the character's state, because asking Lifestream whether it is busy is an IPC call
    // and most trains fail a rule first.
    private static RideVerdict EvaluateAuto(in Announcement announcement, DateTime nowUtc, bool ignoreRunningRide, out Reachability reachability)
    {
        var placement = EvaluatePlacement(announcement, ignoreRunningRide, out reachability);
        if (placement != RideVerdict.Rideable)
        {
            return placement;
        }

        var rules = EvaluateAutoRules(announcement, nowUtc, reachability);
        return rules != RideVerdict.Rideable ? rules : EvaluateCharacter();
    }

    private static RideVerdict EvaluatePlacement(in Announcement announcement, bool ignoreRunningRide, out Reachability reachability)
    {
        reachability = Reachability.OutOfRegion;
        if (announcement.World.Id == 0)
        {
            return RideVerdict.FeedWorldUnknown;
        }

        if (!ignoreRunningRide && Plugin.Instance.Controller.Running)
        {
            return RideVerdict.RideRunning;
        }

        if (!TryReachability(announcement.World, out reachability) || reachability == Reachability.OutOfRegion)
        {
            return RideVerdict.OutOfRegion;
        }

        return RideVerdict.Rideable;
    }

    private static RideVerdict EvaluateCharacter()
    {
        if (InDuty())
        {
            return RideVerdict.InDuty;
        }

        return LifestreamIPC.Instance.IsBusy() ? RideVerdict.LifestreamBusy : RideVerdict.Rideable;
    }

    // Only for a train in the player's region, whose reachability is known; the manual checks are not repeated.
    public static RideVerdict EvaluateAutoRules(in Announcement announcement, DateTime nowUtc, Reachability reachability)
    {
        var configuration = Plugin.Instance.Configuration;
        if (configuration.IsSnoozed(nowUtc))
        {
            return RideVerdict.Snoozed;
        }

        if (!configuration.IsGroupEnabled(announcement.Group))
        {
            return RideVerdict.AutoJoinOff;
        }

        if (!IsAllowedDataCenter(announcement.World))
        {
            return RideVerdict.NotAllowedDataCenter;
        }

        if (reachability == Reachability.CrossDataCenter && !configuration.AllowCrossDataCenterRides)
        {
            return RideVerdict.CrossDataCenterOff;
        }

        return Timing(configuration, announcement.LeadAt(nowUtc), reachability);
    }

    public static bool TryReachability(in WorldInfo target, out Reachability reachability)
    {
        reachability = Reachability.OutOfRegion;
        if (!Worlds.TryCurrent(out var current))
        {
            return false;
        }

        if (current.Id == target.Id)
        {
            reachability = Reachability.SameWorld;
        }
        else if (Worlds.SameDataCenter(current, target))
        {
            reachability = Reachability.SameDataCenter;
        }
        else if (Worlds.SameRegion(current, target))
        {
            reachability = Reachability.CrossDataCenter;
        }

        return true;
    }

    // A data center transfer takes minutes plus a queue, so a train on another data center that starts within the
    // lead, or has already started, cannot be reached in time; and a train in progress has left its start zone.
    private static RideVerdict Timing(Configuration configuration, TimeSpan lead, Reachability reachability)
    {
        if (reachability == Reachability.CrossDataCenter && lead.TotalSeconds < Math.Max(0, configuration.MinimumLeadSeconds))
        {
            return RideVerdict.TooSoon;
        }

        if (lead <= TimeSpan.Zero && -lead > Announcement.InProgressAfter)
        {
            return RideVerdict.TooLate;
        }

        return RideVerdict.Rideable;
    }

    public static bool IsAllowedDataCenter(in WorldInfo world)
    {
        var configuration = Plugin.Instance.Configuration;
        if (configuration.AllowedDataCenters.Length == 0)
        {
            return Worlds.TryHome(out var home) && home.DataCenterId == world.DataCenterId;
        }

        return configuration.ListsDataCenter(world.DataCenterName);
    }

    private static bool InDuty()
        => Svc.Condition[ConditionFlag.BoundByDuty]
        || Svc.Condition[ConditionFlag.BoundByDuty56]
        || Svc.Condition[ConditionFlag.InDutyQueue];

    public static string Explain(RideVerdict verdict) => verdict switch
    {
        RideVerdict.Rideable => "rideable",
        RideVerdict.RideRunning => "a ride is already running",
        RideVerdict.OutOfRegion => "it runs in another region",
        RideVerdict.InDuty => "the character is in a duty or a duty queue",
        RideVerdict.LifestreamBusy => "Lifestream is busy",
        RideVerdict.Snoozed => "auto-join is snoozed",
        RideVerdict.AutoJoinOff => "auto-join is off for its expansion group",
        RideVerdict.NotAllowedDataCenter => "its data center is not an allowed one",
        RideVerdict.CrossDataCenterOff => "auto-join across data centers is off",
        RideVerdict.SRankCrossDataCenter => "S-Hunts can visit another world on this data center, but not another data center",
        RideVerdict.TooSoon => "it starts sooner than the lead time a data center transfer needs",
        RideVerdict.TooLate => "it is already in progress",
        _ => "its world is unknown",
    };
}
