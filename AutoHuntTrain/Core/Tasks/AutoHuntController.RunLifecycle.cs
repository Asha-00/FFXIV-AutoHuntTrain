using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Game.Ops;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Core.Tasks;

internal sealed partial class AutoHuntController
{
    private const int MaxFaultResumes = 3;
    private const int MaxFaultResumesPerRun = 6;
    private const long FaultResumeWindowMs = 5 * TimeUnits.MillisecondsPerMinute;
    // Long enough to cover the next train announced on the data center, short enough not to idle for long.
    private static readonly TimeSpan NextTrainWindow = TimeSpan.FromMinutes(20);

    private int faultResumeCount;
    private int runFaultResumeCount;
    private long faultWindowStartedAtMs;

    private void OnRideEnded(AutoHuntSession owningSession)
    {
        if (CutByRelog(owningSession))
        {
            return;
        }

        var faulted = ReferenceEquals(session, owningSession) && owningSession.EndedWithFault;
        if (faulted && !owningSession.CompletedByStopCondition && TryAutoResumeAfterFault(owningSession))
        {
            return;
        }

        if (faulted)
        {
            ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} The ride stopped on an unexpected error. The log has the details.");
        }

        EndRun(owningSession);
    }

    // A ride that ended without deciding how, while the character is logged out with a ride saved for the login, was
    // taken down by the transfer's relog. It is neither recorded nor cleared: the login rebuilds it with its start and
    // its credits, and the rebuilt ride is recorded once, as the same ride.
    private bool CutByRelog(AutoHuntSession owningSession)
    {
        if (!ReferenceEquals(session, owningSession)
            || owningSession.Outcome is not null
            || owningSession.EndedWithFault
            || ECommons.DalamudServices.Svc.ClientState.IsLoggedIn
            || Plugin.Instance.Configuration.PendingRide is null)
        {
            return false;
        }

        owningSession.Recorded = true;
        ReleaseHelpers();
        ClearRun();
        Diag("The ride task ended while logged out for the data center transfer; the saved ride is picked up at login.");
        return true;
    }

    private void EndRun(AutoHuntSession owningSession)
    {
        if (ReferenceEquals(session, owningSession))
        {
            Plugin.Instance.Configuration.ClearPendingRide();
        }

        FinalizeRun(owningSession, sample: true);
        if (!ReferenceEquals(session, owningSession))
        {
            Diag("A run that is no longer live ended; it was recorded and the current run is left alone.");
            return;
        }

        progress.ClearMark();
        progress.ClearFlag();
        if (!TryRunAfterAction(owningSession))
        {
            ClearRun();
        }
    }

    // One ordered chain once a ride ends on its own: leaving the train's party, the way home, then the after-run action.
    // A Stop or a fault runs none of them. The finished run stays on screen while the chain runs, and is cleared once it
    // ends.
    private bool TryRunAfterAction(AutoHuntSession ending)
    {
        if (ending.AfterActionDispatched)
        {
            return false;
        }

        if (!ending.CompletedByStopCondition || ending.EndedWithFault)
        {
            Diag($"Run ended without meeting its stop condition (fault {ending.EndedWithFault}); no way home and no after-run action.");
            return false;
        }

        ending.AfterActionDispatched = true;
        if (!ECommons.DalamudServices.Svc.ClientState.IsLoggedIn)
        {
            Diag("Run ended while logged out; no way home and no after-run action.");
            return false;
        }

        var leaveParty = LeavePartyDue(ending);
        var stays = StaysForNextTrain();
        var action = stays ? null : AfterActionFor(ending);
        var wayHome = !stays && WayHomeDue();
        if (!leaveParty && !wayHome && action is null)
        {
            return false;
        }

        progress.SetPhase(HuntPhase.Finishing);
        RunChain(leaveParty, wayHome, action);
        return true;
    }

    private void RunChain(bool leaveParty, bool wayHome, AfterRunAction? action)
    {
        if (leaveParty)
        {
            RunTask(new AutoLeaveParty(), () => RunChain(leaveParty: false, wayHome, action));
            return;
        }

        if (wayHome)
        {
            Diag(action is { } then ? $"Run completed; taking the way home, then after-run action {then}." : "Run completed; taking the way home.");
            RunTask(new AutoWayHome(), () =>
            {
                Diag("The way home finished.");
                RunChain(leaveParty: false, wayHome: false, action);
            });
            return;
        }

        if (action is { } next)
        {
            RunAfterAction(next);
            return;
        }

        ClearRun();
    }

    // Only a party joined during the ride belongs to the train; one the character was in before it is the player's own.
    private static bool LeavePartyDue(AutoHuntSession ending)
    {
        if (!Plugin.Instance.Configuration.LeavePartyAfterRide || !PartyOps.InParty())
        {
            return false;
        }

        if (ending.InPartyAtStart)
        {
            Diag("The ride is over; the party the character was in before it started is kept.");
            return false;
        }

        return true;
    }

    private static AfterRunAction? AfterActionFor(AutoHuntSession ending)
    {
        var action = Plugin.Instance.Configuration.AfterRun;
        if (action == AfterRunAction.StayLoggedIn)
        {
            Diag("Run completed by its stop condition; the after-run action is StayLoggedIn, nothing to do.");
            return null;
        }

        if (ending.DidNothing)
        {
            Diag($"Run ended by its stop condition without doing any work; skipping after-run action {action}.");
            return null;
        }

        return action;
    }

    private void RunAfterAction(AfterRunAction action)
    {
        Diag($"Starting after-run action {action}.");
        AutoCommon task = action == AfterRunAction.ReturnToInn ? new AutoReturnToInn() : new AutoAfterRun(action);
        RunTask(task, () =>
        {
            Diag($"After-run action {action} finished.");
            ClearRun();
        });
    }

    private static bool WayHomeDue()
    {
        if (!Plugin.Instance.Configuration.ReturnHomeAfterRide)
        {
            return false;
        }

        if (!Worlds.TryHome(out var home) || !Worlds.TryCurrent(out var current))
        {
            Diag("The way home is on, but the home or current world could not be read; staying.");
            return false;
        }

        if (home.Id == current.Id)
        {
            Diag($"Already on the home world {home.Name}; no way home needed.");
            return false;
        }

        return true;
    }

    // With auto-join on, a train on this data center that auto-join would take soon is worth staying for: the way home
    // would carry the character off, and the after-run action would log it out or park it at the inn.
    private bool StaysForNextTrain()
    {
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.StayForNextTrain || !configuration.IsAutoJoinActive())
        {
            return false;
        }

        var nowUtc = DateTime.UtcNow;
        var feed = Plugin.Instance.Feed;
        for (var index = 0; index < feed.Count; index++)
        {
            var announcement = feed[index];
            // S ranks are manual one-off hunts. They must never make a completed run stay on another world waiting for
            // train auto-join, even when their compatibility StartAtUtc falls inside the next-train window.
            if (!announcement.IsTrain
                || announcement.Id == lastRiddenAnnouncementId
                || announcement.LeadAt(nowUtc) > NextTrainWindow)
            {
                continue;
            }

            if (RideRules.EvaluateNextTrain(announcement, nowUtc, out var reachability) != RideVerdict.Rideable
                || reachability is not (Reachability.SameWorld or Reachability.SameDataCenter))
            {
                continue;
            }

            var group = ExpansionGroups.Name(announcement.Group);
            Diag($"Staying for the {group} train on {announcement.World.Name}, starting in {announcement.LeadAt(nowUtc).TotalMinutes:F0} min; no way home and no after-run action, auto-join takes it.");
            ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Staying for the {group} train on {announcement.World.Name}.");
            return true;
        }

        return false;
    }

    // Idempotent through Recorded, so an explicit Stop and a finished task can both call it.
    private void FinalizeRun(AutoHuntSession? ending, bool sample)
    {
        if (ending is null || ending.Recorded)
        {
            return;
        }

        ending.Recorded = true;
        ending.End();
        try
        {
            if (sample)
            {
                ending.Sample();
            }

            var outcome = ending.Outcome ?? (ending.EndedWithFault ? RideOutcome.Faulted : RideOutcome.Stopped);
            // A ride stopped before it did anything is a misclick more often than a ride; any other end is worth a row.
            if (ending.DidNothing && outcome == RideOutcome.Stopped)
            {
                Diag("Run was stopped before doing any work; nothing recorded to history.");
                return;
            }

            var record = new RunRecord
            {
                StartedAtUtc = ending.StartedAt,
                EndedAtUtc = DateTime.UtcNow,
                DurationSeconds = ending.Elapsed.TotalSeconds,
                WorldName = ending.WorldName,
                DataCenterName = ending.DataCenterName,
                Expansion = ending.Expansion,
                Group = ending.Group ?? (ending.Expansion is { } kind ? ExpansionGroups.FromExpansionKind(kind) : null),
                IsSRank = ending.IsSRank,
                CrossedDataCenter = ending.CrossedDataCenter,
                Outcome = outcome,
                MarksCredited = ending.MarksCredited,
                AlliedSeals = ending.AlliedSeals,
                CenturioSeals = ending.CenturioSeals,
                Nuts = ending.Nuts,
                JobAbbreviation = ending.JobAbbreviation,
            };
            Plugin.Instance.History.Append(record);
            Diag($"Run recorded to history ({outcome}): {record.MarksCredited} marks credited, {record.AlliedSeals} allied seals, {record.CenturioSeals} centurio seals, {record.Nuts} nuts over {record.Duration} as {record.JobAbbreviation} on {record.WorldName} ({record.DataCenterName}), {record.Group?.ToString() ?? "unknown group"}{(record.CrossedDataCenter ? ", across data centers" : string.Empty)}.");
        }
        catch (Exception exception)
        {
            Diag($"FinalizeRun failed to record history: {exception.Message}");
        }
    }

    private void ResetFaultBudget()
    {
        faultResumeCount = 0;
        runFaultResumeCount = 0;
        faultWindowStartedAtMs = 0;
    }

    // The window restarts once it lapses, so sparse faults over a long run each get a fresh budget; only a burst, a wedge
    // that faults again straight away, spends it. The per-run cap still ends a run whose faults keep coming, however far
    // apart they are.
    private bool TryAutoResumeAfterFault(AutoHuntSession owningSession)
    {
        if (!Plugin.Instance.Configuration.AutoResumeOnFault)
        {
            Diag("Ride task faulted and auto-resume on fault is off; the run ends.");
            return false;
        }

        if (!CanRestart())
        {
            Diag("Ride task faulted with nothing to resume; the run ends.");
            return false;
        }

        if (runFaultResumeCount >= MaxFaultResumesPerRun)
        {
            Diag($"Ride task faulted after {runFaultResumeCount} restarts in this run; not resuming. The run ends.");
            return false;
        }

        var now = Environment.TickCount64;
        if (now - faultWindowStartedAtMs > FaultResumeWindowMs)
        {
            faultResumeCount = 0;
            faultWindowStartedAtMs = now;
        }

        if (faultResumeCount >= MaxFaultResumes)
        {
            Diag($"Ride task faulted {faultResumeCount} times within {FaultResumeWindowMs / TimeUnits.MillisecondsPerMinute} minutes; not resuming. The run ends.");
            return false;
        }

        faultResumeCount++;
        runFaultResumeCount++;
        owningSession.ClearFault();
        Diag($"Ride task ended on an unexpected fault; auto-resuming (resume {faultResumeCount}/{MaxFaultResumes} in this {FaultResumeWindowMs / TimeUnits.MillisecondsPerMinute} minute window, {runFaultResumeCount}/{MaxFaultResumesPerRun} in this run).");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} The ride stopped on an unexpected error; restarting it ({faultResumeCount}/{MaxFaultResumes}).");
        RunRide(owningSession);
        return true;
    }
}
