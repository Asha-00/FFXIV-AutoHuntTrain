using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Core.Tasks;

// Starts one-off S-rank runs without adding conductor or route branches to the normal train controller path.
internal sealed partial class AutoHuntController
{
    // Shared entry point for feed and details buttons. Existing train behaviour stays in StartRide; an S rank receives
    // its own task because it has one reported target and no conductor, flags, catch-up route or expected mark total.
    public bool StartAnnouncement(Announcement announcement)
        => announcement.IsSRank
            ? StartSRank(announcement)
            : StartRide(announcement);

    public bool StartSRank(Announcement announcement)
    {
        var world = announcement.World;
        var creature = announcement.CreatureNameId is > 0
            ? HuntMarkRegistry.NameOf(announcement.CreatureNameId.Value)
            : "the S rank";

        if (!announcement.IsSRank)
        {
            Diag($"S-hunt of {creature} on {world.Name} refused: the announcement is not an S rank.");
            return false;
        }

        if (Running)
        {
            Diag($"S-hunt of {creature} on {world.Name} ignored: a ride is already running.");
            return false;
        }

        if (!RequiredPluginsReady())
        {
            return false;
        }

        if (!RideRules.TryReachability(announcement.World, out var reachability))
        {
            return RefuseSRank(announcement, creature, "the announced world is not one this client knows");
        }

        if (reachability == Reachability.CrossDataCenter)
        {
            return RefuseSRank(announcement, creature, "cross-data-center S-Hunts are not supported");
        }

        var verdict = RideRules.EvaluateManual(announcement, out _);
        if (verdict != RideVerdict.Rideable)
        {
            var explanation = RideRules.Explain(verdict);
            Diag($"S-hunt of {creature} on {world.Name} refused: {explanation}.");
            ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Cannot hunt {creature} on {world.Name}: {explanation}.");
            return false;
        }

        if (announcement.CreatureNameId is not > 0)
        {
            return RefuseSRank(announcement, creature, "the alert names no creature");
        }

        if (!HuntMarkRegistry.TryGet(announcement.CreatureNameId.Value, out var mark) || mark.Rank != HuntMarkRank.S)
        {
            return RefuseSRank(announcement, creature, "the alert's creature is not a registered S rank");
        }

        if (!announcement.NamesTerritory)
        {
            return RefuseSRank(announcement, creature, "the alert names no known territory");
        }

        if (announcement.MapCoordinates is null)
        {
            return RefuseSRank(announcement, creature, "the alert contains no usable map coordinates");
        }

        lastRiddenAnnouncementId = announcement.Id;
        RememberTaken(announcement.Id);
        BeginRun(
            NewRideSession(announcement),
            owning => new AutoSRank(owning, progress, announcement),
            $"hunting S rank {creature} on {world.Name} ({world.DataCenterName})");

        var destination = announcement.NamesAetheryte
            ? StartText(announcement)
            : announcement.NamesTerritory
                ? TerritoryNames.Of(announcement.TerritoryId)
                : world.Name;
        RideChat.PrintStart($"{AhtConstants.LogPrefix} Hunting {creature} on {world.Name}; travelling to {destination}, then waiting for another player to pull.");
        return true;
    }

    private static bool RefuseSRank(in Announcement announcement, string creature, string reason)
    {
        Diag($"S-hunt of {creature} on {announcement.World.Name} refused: {reason}.");
        ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Cannot hunt {creature} on {announcement.World.Name}: {reason}.");
        return false;
    }
}
