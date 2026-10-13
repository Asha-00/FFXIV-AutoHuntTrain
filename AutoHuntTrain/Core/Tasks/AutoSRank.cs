using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// Rides one HuntAlerts S-rank relay: travel to its world, aetheryte and instance; locate and stage safely near
// the exact live creature; wait for another player to pull it; join that fight; then end after credit is settled.
// There is deliberately no conductor, flag listener, catch-up route or fallback sweep over known spawn points.
internal sealed class AutoSRank : AutoCommon
{
    private const string Scope = "s-rank";

    private readonly AutoHuntSession session;
    private readonly RideProgress progress;
    private readonly Announcement announcement;

    public AutoSRank(AutoHuntSession session, RideProgress progress, Announcement announcement)
    {
        this.session = session;
        this.progress = progress;
        this.announcement = announcement;
    }

    protected override async Task Execute()
    {
        await HoldCombatMovementAndSettle(Scope);
        try
        {
            if (!TryReadTarget(out var nameId, out var reportedPoint))
            {
                EndAbandoned("the announcement no longer contains a usable S-rank target and territory");
                return;
            }

            var creature = HuntMarkRegistry.NameOf(nameId);
            if (!await CompleteJourney(creature))
            {
                return;
            }

            if (CancelToken.IsCancellationRequested)
            {
                return;
            }

            progress.SetStartTerritory(announcement.TerritoryId);
            if (reportedPoint is { } point)
            {
                progress.SetMark(TrainMark.Flagged(nameId, announcement.TerritoryId, point.X, point.Z));
            }

            progress.SetCredits(session.MarksCredited, 1);
            Status = $"Searching for {creature}";
            Diag(
                reportedPoint is null
                    ? $"S rank: the relay has no coordinates; " +
                      $"resolving {creature}'s live position after arrival in " +
                      $"{TerritoryNames.Of(announcement.TerritoryId)} " +
                      $"(BNpcName {nameId})"
                    : $"S rank: searching the reported location in " +
                      $"{TerritoryNames.Of(announcement.TerritoryId)} " +
                      $"for {creature} (BNpcName {nameId})");
            var outcome = await HuntSRank(nameId, announcement.TerritoryId, reportedPoint);
            ResolveMarkOutcome(creature, outcome);
        }
        catch (Exception exception)
        {
            session.RecordFault(exception, CancelToken);
            throw;
        }
        finally
        {
            ReleaseCombatMovement(Scope);
        }
    }

    // The hunt's finer phase drives the existing status panels while the one-off task searches, waits and fights.
    private protected override void OnMarkPhaseChanged(HuntPhase phase)
    {
        if (phase != HuntPhase.Idle)
        {
            progress.SetPhase(phase);
        }
    }

    private bool TryReadTarget(out uint nameId, out Vector3? reportedPoint)
    {
        nameId = 0;
        reportedPoint = null;
        if (announcement.CreatureNameId is not > 0
            || !HuntMarkRegistry.TryGet(announcement.CreatureNameId.Value, out var mark)
            || mark.Rank != HuntMarkRank.S
            || !announcement.NamesTerritory)
        {
            return false;
        }

        if (announcement.MapCoordinates is { } map)
        {
            if (!MapCoordinates.TryToWorld(announcement.TerritoryId, map, out var point))
            {
                return false;
            }

            reportedPoint = point;
        }

        nameId = announcement.CreatureNameId.Value;
        return true;
    }

    private async Task<bool> CompleteJourney(string creature)
    {
        progress.SetRidePhase(RidePhase.Journey);
        Status = $"Travelling to {creature}";
        var journey = JourneyPlan.ToWorld(
            announcement.World.Name,
            announcement.AetheryteId,
            announcement.TerritoryId,
            announcement.Instance);

        var outcome = await TravelToWorld(
            journey,
            resumed: false,
            giveUpAtUtc:
                announcement.PostedAtUtc
                + SRankFeedPolicy.ListedAfterReport);

        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        if (outcome == JourneyOutcome.Arrived)
        {
            session.ArrivedAtTrain = true;
            Diag($"S rank: arrived on {announcement.World.Name} in {TerritoryNames.Of(announcement.TerritoryId)}{InstanceText()}");
            return true;
        }

        EndOnJourney(creature, outcome);
        return false;
    }

    private void ResolveMarkOutcome(string creature, MarkOutcome outcome)
    {
        if (CancelToken.IsCancellationRequested || outcome == MarkOutcome.Cancelled)
        {
            return;
        }

        session.CompletedByStopCondition = true;
        Status = "S-Hunt ended";

        if (outcome == MarkOutcome.Killed)
        {
            session.CreditMark();
            progress.SetCredits(session.MarksCredited, 1);
            session.Outcome = RideOutcome.AllCredited;
            Diag($"S rank: {creature} credited; the one-off hunt ends");
            Svc.Chat.Print($"{AhtConstants.LogPrefix} {creature} is credited; the S-Hunt ends.");
            return;
        }

        session.Outcome = RideOutcome.Abandoned;
        Diag($"S rank: {creature} ended with {outcome}; no credit was recorded");
        Svc.Chat.PrintError($"{AhtConstants.LogPrefix} The S-Hunt for {creature} ended without credit ({Describe(outcome)}).");
    }

    private void EndOnJourney(string creature, JourneyOutcome outcome)
    {
        session.Outcome = RideOutcome.Abandoned;
        session.CompletedByStopCondition = outcome != JourneyOutcome.Cancelled;
        Status = "S-Hunt travel ended";
        Diag($"S rank: the journey to {creature} on {announcement.World.Name} ended with {outcome}");
        if (outcome != JourneyOutcome.Cancelled)
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Could not reach {creature} on {announcement.World.Name} ({Describe(outcome)}).");
        }
    }

    private void EndAbandoned(string reason)
    {
        session.Outcome = RideOutcome.Abandoned;
        session.CompletedByStopCondition = !CancelToken.IsCancellationRequested;
        Status = "S-Hunt ended";
        Warn($"S rank: {reason}");
        if (!CancelToken.IsCancellationRequested)
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} The S-Hunt ended: {reason}.");
        }
    }

    private string InstanceText()
        => announcement.NamesInstance ? $" instance {announcement.Instance}" : string.Empty;

    private static string Describe(JourneyOutcome outcome) => outcome switch
    {
        JourneyOutcome.Refused => "travel was refused",
        JourneyOutcome.WorldUnreached => "the world could not be reached",
        JourneyOutcome.AetheryteUnreached => "the aetheryte could not be reached",
        JourneyOutcome.InstanceUnreached => "the instance could not be reached",
        JourneyOutcome.Overdue => "the relay expired during travel",
        JourneyOutcome.Cancelled => "cancelled",
        _ => outcome.ToString(),
    };

    private static string Describe(MarkOutcome outcome) => outcome switch
    {
        MarkOutcome.NotFound => "the mark was not found in the announced zone or at the reported location",
        MarkOutcome.Died => "the character died",
        MarkOutcome.Unsupported => "the mark or territory is unsupported",
        MarkOutcome.Unreachable => "the reported location could not be reached",
        MarkOutcome.KillsNotCounted => "the mark died without recorded credit",
        MarkOutcome.CombatUnavailable => "the combat helper was unavailable",
        MarkOutcome.Cancelled => "cancelled",
        _ => outcome.ToString(),
    };
}
