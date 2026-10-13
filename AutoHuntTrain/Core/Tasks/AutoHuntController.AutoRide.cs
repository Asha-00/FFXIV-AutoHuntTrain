using AutoHuntTrain.Core.Feed;

namespace AutoHuntTrain.Core.Tasks;

internal sealed partial class AutoHuntController
{
    private const int FeedEvaluationIntervalMs = 1_000;

    // Every announcement auto-join or the player has already taken, as many as the feed can hold, so auto-join never
    // swaps back and forth between two trains when the player stops each one. Manual S-Hunts use the same taken ring.
    private readonly int[] takenAnnouncementIds = new int[FeedListener.Capacity];

    private long nextFeedEvaluationAtMs;
    private int lastRiddenAnnouncementId;
    private int autoRideRefusedId;
    private int nextTakenSlot;
    private int autoRideHoldBelowId;

    // The player stopping a ride is a decision about auto-join too: it waits for a train announced after the stop
    // instead of taking the next one in the list a second later.
    public void StopByPlayer()
    {
        var riding = Running;
        Stop();
        if (!riding || !Plugin.Instance.Configuration.IsAutoJoinActive())
        {
            return;
        }

        autoRideHoldBelowId = Plugin.Instance.Feed.NextId;
        Diag("Auto-join: the player stopped the ride; waiting for a train announced from now on.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Auto-join will wait for the next train announced from now on.");
    }

    private void RememberTaken(int announcementId)
    {
        takenAnnouncementIds[nextTakenSlot] = announcementId;
        nextTakenSlot = (nextTakenSlot + 1) % takenAnnouncementIds.Length;
    }

    // Whether auto-join leaves this train alone whatever the rules say: taken already, refused at its start, or
    // announced before the player's last Stop.
    public bool AutoJoinSkips(int announcementId)
        => announcementId < autoRideHoldBelowId || announcementId == autoRideRefusedId || WasTaken(announcementId);

    private bool WasTaken(int announcementId)
    {
        for (var index = 0; index < takenAnnouncementIds.Length; index++)
        {
            if (takenAnnouncementIds[index] == announcementId)
            {
                return true;
            }
        }

        return false;
    }

    // Once a second while any expansion's auto-join is on, nothing runs and no Party Finder listing is still coming down:
    // the soonest announced train that passes every rule is ridden. A train already taken, one whose start was refused,
    // and anything announced before the player's last Stop are left alone. Nothing here allocates unless a ride starts.
    private void TickAutoRide(long now)
    {
        if (now < nextFeedEvaluationAtMs)
        {
            return;
        }

        nextFeedEvaluationAtMs = now + FeedEvaluationIntervalMs;
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.IsAutoJoinActive()
            || Running
            || !ECommons.DalamudServices.Svc.ClientState.IsLoggedIn
            || Plugin.Instance.PartyFinderListing.Busy)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (configuration.IsSnoozed(nowUtc))
        {
            return;
        }

        var feed = Plugin.Instance.Feed;
        for (var index = 0; index < feed.Count; index++)
        {
            var announcement = feed[index];
            // S ranks share the feed and manual action UI, but train auto-join must never start one implicitly.
            if (announcement.IsSRank)
            {
                continue;
            }

            if (AutoJoinSkips(announcement.Id))
            {
                continue;
            }

            if (RideRules.EvaluateAuto(announcement, nowUtc, out _) != RideVerdict.Rideable)
            {
                continue;
            }

            Diag($"Auto-join: the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} starts in {announcement.LeadAt(nowUtc).TotalMinutes:F0} min and passes every rule; riding it.");
            if (!StartRide(announcement))
            {
                autoRideRefusedId = announcement.Id;
            }

            return;
        }
    }
}
