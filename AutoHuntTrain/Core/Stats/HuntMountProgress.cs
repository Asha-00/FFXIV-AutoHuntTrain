using AutoHuntTrain.Core.Feed;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System.Text;

namespace AutoHuntTrain.Core.Stats;

internal enum MountProgressStatus : byte
{
    Waiting,
    Reading,
    Ready,
    Partial,
}

internal enum CounterStatus : byte
{
    Unknown,
    Counted,
    Complete,
}

internal readonly record struct CounterReading(uint Current, uint Needed, CounterStatus Status)
{
    public float Fraction => Status switch
    {
        CounterStatus.Complete => 1f,
        CounterStatus.Counted when Needed > 0 => Math.Min(1f, Current / (float)Needed),
        _ => 0f,
    };
}

// The game holds the progress of one achievement at a time, so the eight hunt kill counters are read in turn on the
// framework thread: a request, then that counter's id in the slot, or a timeout that leaves the counter to the next
// pass. A pass runs while the History page is open and the readings are older than the refresh interval, and shortly
// after every recorded ride. A counter the completion list already marks complete is not asked for.
internal sealed class HuntMountProgress
{
    private const long RefreshIntervalMs = 5 * 60_000;
    private const long RetryIntervalMs = 30_000;
    private const long AfterRideDelayMs = 5_000;
    private const long ShownGraceMs = 1_000;
    private const long CompletionTimeoutMs = 10_000;
    private const long CounterTimeoutMs = 5_000;
    private const long RequestSpacingMs = 250;
    // A request that leaves the slot alone keeps an older answer for the same id in it; that answer is taken only once
    // the reply has had time to land over it.
    private const long StaleSlotSettleMs = 2_000;

    private const string NotLoggedIn = "no character is logged in";
    private const string Zoning = "the character is between areas";
    private const string AchievementWindowOpen = "the game's Achievements window is open";
    private const string NoAchievementData = "the game's achievement data is not available";

    private enum Step : byte
    {
        Idle,
        LoadingCompletion,
        Requesting,
        AwaitingCounter,
    }

    private readonly RunHistory history;
    private readonly CounterReading[] readings = new CounterReading[HuntMounts.CounterCount];
    private readonly bool[] mountsComplete = new bool[HuntMounts.Count];

    private Step step;
    private MountProgressStatus outcome;
    private int counterIndex;
    private int readCount;
    private int missedCount;
    private int seenHistoryRevision;
    private long passStartedAtMs;
    private long stepStartedAtMs;
    private long nextRequestAtMs;
    private long nextPassAtMs;
    private long shownUntilMs;
    private long faultedUntilMs;
    private bool completionRead;
    private bool slotCleared;
    private bool rideRecorded;
    private ulong contentId;
    private string? deferral;

    public HuntMountProgress(RunHistory history)
    {
        this.history = history;
        seenHistoryRevision = history.Revision;
        Forget();
    }

    public MountProgressStatus Status => step == Step.Idle ? outcome : MountProgressStatus.Reading;

    // When a pass last read the completion list; default until one has.
    public DateTime LastRefreshUtc { get; private set; }

    public bool HasReadings => LastRefreshUtc != default;

    public CounterReading ARank(int mountIndex) => readings[HuntMounts.ARankCounter(mountIndex)];

    public CounterReading SRank(int mountIndex) => readings[HuntMounts.SRankCounter(mountIndex)];

    public bool IsMountComplete(int mountIndex) => mountsComplete[mountIndex];

    // The page calls this on every frame it is drawn and the tick reads it, so drawing never touches game state.
    public void NoteShown() => shownUntilMs = Environment.TickCount64 + ShownGraceMs;

    // A failure must not throw into the framework handler or repeat on every frame, so it parks the reader for a while.
    public void Tick()
    {
        var now = Environment.TickCount64;
        if (now < faultedUntilMs)
        {
            return;
        }

        try
        {
            Advance(now);
        }
        catch (Exception exception)
        {
            step = Step.Idle;
            outcome = MountProgressStatus.Partial;
            faultedUntilMs = now + RefreshIntervalMs;
            RunLog.Warning(exception, "Hunt mount progress: reading the counters failed; trying again in five minutes");
        }
    }

    private unsafe void Advance(long now)
    {
        NoteRecordedRide(now);
        if (step == Step.Idle && now > shownUntilMs && !rideRecorded)
        {
            return;
        }

        if (Svc.ClientState.IsLoggedIn)
        {
            ForgetOtherCharacter();
        }

        if (step == Step.Idle && now < nextPassAtMs)
        {
            return;
        }

        var achievement = CanQuery(out var reason) ? Achievement.Instance() : null;
        if (achievement == null)
        {
            reason = reason.Length == 0 ? NoAchievementData : reason;
            if (step == Step.Idle)
            {
                Defer(reason);
                return;
            }

            EndPass(now, reason);
            return;
        }

        switch (step)
        {
            case Step.Idle:
                BeginPass(achievement, now);
                return;
            case Step.LoadingCompletion:
                AwaitCompletion(achievement, now);
                return;
            case Step.Requesting:
                RequestNextCounter(achievement, now);
                return;
            case Step.AwaitingCounter:
                AwaitCounter(achievement, now);
                return;
        }
    }

    // The history only changes when a ride is recorded or cleared, and a recorded ride is what moves the counters.
    private void NoteRecordedRide(long now)
    {
        var revision = history.Revision;
        if (revision == seenHistoryRevision)
        {
            return;
        }

        seenHistoryRevision = revision;
        rideRecorded = true;
        nextPassAtMs = Math.Min(nextPassAtMs, now + AfterRideDelayMs);
        RunLog.Debug("Hunt mount progress: the run history changed; the counters are read again in five seconds.");
    }

    private void ForgetOtherCharacter()
    {
        var current = Svc.PlayerState.ContentId;
        if (current == 0 || current == contentId)
        {
            return;
        }

        if (contentId != 0)
        {
            RunLog.Info("Hunt mount progress: another character logged in; its counters are read afresh.");
            Forget();
        }

        contentId = current;
    }

    private void Forget()
    {
        for (var index = 0; index < readings.Length; index++)
        {
            readings[index] = new CounterReading(0, HuntMounts.CounterNeeded(index), CounterStatus.Unknown);
        }

        Array.Clear(mountsComplete);
        LastRefreshUtc = default;
        outcome = MountProgressStatus.Waiting;
        step = Step.Idle;
        nextPassAtMs = 0;
    }

    private static unsafe bool CanQuery(out string reason)
    {
        if (!Svc.ClientState.IsLoggedIn || Svc.Objects.LocalPlayer is null)
        {
            reason = NotLoggedIn;
            return false;
        }

        if (Svc.Condition[ConditionFlag.BetweenAreas] || Svc.Condition[ConditionFlag.BetweenAreas51])
        {
            reason = Zoning;
            return false;
        }

        // The game's window asks for progress through the same slot; reading under it would cost it its answer.
        var agent = AgentAchievement.Instance();
        if (agent != null && agent->IsAgentActive())
        {
            reason = AchievementWindowOpen;
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void Defer(string reason)
    {
        if (ReferenceEquals(deferral, reason))
        {
            return;
        }

        deferral = reason;
        RunLog.Debug($"Hunt mount progress: holding off while {reason}.");
    }

    private unsafe void BeginPass(Achievement* achievement, long now)
    {
        step = Step.LoadingCompletion;
        passStartedAtMs = now;
        stepStartedAtMs = now;
        counterIndex = 0;
        readCount = 0;
        missedCount = 0;
        completionRead = false;
        rideRecorded = false;
        deferral = null;

        var state = achievement->State;
        if (state == Achievement.AchievementState.Loaded)
        {
            RunLog.Debug("Hunt mount progress: reading the hunt counters; the completion list is already loaded.");
            return;
        }

        achievement->RequestCompletedAchievements();
        RunLog.Debug($"Hunt mount progress: reading the hunt counters; asked for the completion list, which was {state}.");
    }

    private unsafe void AwaitCompletion(Achievement* achievement, long now)
    {
        if (achievement->State != Achievement.AchievementState.Loaded)
        {
            if (now - stepStartedAtMs >= CompletionTimeoutMs)
            {
                EndPass(now, "the completion list did not arrive within ten seconds");
            }

            return;
        }

        for (var mountIndex = 0; mountIndex < HuntMounts.Count; mountIndex++)
        {
            mountsComplete[mountIndex] = achievement->IsComplete(HuntMounts.All[mountIndex].AchievementId);
        }

        for (var index = 0; index < readings.Length; index++)
        {
            if (!achievement->IsComplete((int)HuntMounts.CounterAchievementId(index)))
            {
                continue;
            }

            var needed = HuntMounts.CounterNeeded(index);
            readings[index] = new CounterReading(needed, needed, CounterStatus.Complete);
        }

        completionRead = true;
        step = Step.Requesting;
        nextRequestAtMs = now;
    }

    private unsafe void RequestNextCounter(Achievement* achievement, long now)
    {
        if (now < nextRequestAtMs)
        {
            return;
        }

        while (counterIndex < readings.Length && readings[counterIndex].Status == CounterStatus.Complete)
        {
            counterIndex++;
        }

        if (counterIndex >= readings.Length)
        {
            EndPass(now, null);
            return;
        }

        var id = HuntMounts.CounterAchievementId(counterIndex);
        achievement->RequestAchievementProgress(id);
        slotCleared = !SlotHolds(achievement, id);
        stepStartedAtMs = now;
        step = Step.AwaitingCounter;
        RunLog.Debug($"Hunt mount progress: asked for counter {id}; the slot now reads {achievement->ProgressRequestState} for {achievement->ProgressAchievementId}.");
    }

    private unsafe void AwaitCounter(Achievement* achievement, long now)
    {
        var id = HuntMounts.CounterAchievementId(counterIndex);
        if (!SlotHolds(achievement, id))
        {
            slotCleared = true;
            if (now - stepStartedAtMs < CounterTimeoutMs)
            {
                return;
            }

            missedCount++;
            RunLog.Info($"Hunt mount progress: counter {id} did not answer within five seconds; it is read again on the next pass.");
            NextCounter(now);
            return;
        }

        if (!slotCleared && now - stepStartedAtMs < StaleSlotSettleMs)
        {
            return;
        }

        var current = achievement->ProgressCurrent;
        var max = achievement->ProgressMax;
        readings[counterIndex] = new CounterReading(current, max > 0 ? max : HuntMounts.CounterNeeded(counterIndex), CounterStatus.Counted);
        readCount++;
        RunLog.Debug($"Hunt mount progress: counter {id} reads {current}/{max}{(slotCleared ? string.Empty : " (the slot never cleared, so this may be its earlier answer)")}.");
        NextCounter(now);
    }

    private static unsafe bool SlotHolds(Achievement* achievement, uint id)
        => achievement->ProgressRequestState == Achievement.AchievementState.Loaded && achievement->ProgressAchievementId == id;

    private void NextCounter(long now)
    {
        counterIndex++;
        step = Step.Requesting;
        nextRequestAtMs = now + RequestSpacingMs;
    }

    private void EndPass(long now, string? failure)
    {
        step = Step.Idle;
        for (var mountIndex = 0; mountIndex < HuntMounts.Count; mountIndex++)
        {
            if (readings[HuntMounts.ARankCounter(mountIndex)].Status == CounterStatus.Complete && readings[HuntMounts.SRankCounter(mountIndex)].Status == CounterStatus.Complete)
            {
                mountsComplete[mountIndex] = true;
            }
        }

        var failed = failure is not null || missedCount > 0;
        outcome = failed ? MountProgressStatus.Partial : MountProgressStatus.Ready;
        if (completionRead)
        {
            LastRefreshUtc = DateTime.UtcNow;
        }

        nextPassAtMs = now + (rideRecorded ? AfterRideDelayMs : failed ? RetryIntervalMs : RefreshIntervalMs);
        var elapsed = now - passStartedAtMs;
        if (failure is not null)
        {
            RunLog.Info($"Hunt mount progress: pass stopped after {elapsed} ms because {failure}; {readCount} counters read, retrying in {(nextPassAtMs - now) / 1000} s. {Summary()}");
            return;
        }

        if (missedCount > 0 && readCount == 0)
        {
            RunLog.Warning($"Hunt mount progress: none of the {missedCount} counters asked for answered; retrying in {(nextPassAtMs - now) / 1000} s.");
            return;
        }

        RunLog.Info($"Hunt mount progress: pass done in {elapsed} ms, {readCount} counters read, {missedCount} missed. {Summary()}");
    }

    private string Summary()
    {
        var builder = new StringBuilder();
        for (var mountIndex = 0; mountIndex < HuntMounts.Count; mountIndex++)
        {
            if (mountIndex > 0)
            {
                builder.Append("; ");
            }

            builder.Append(ExpansionGroups.Name(HuntMounts.All[mountIndex].Group)).Append(mountsComplete[mountIndex] ? " (earned)" : string.Empty);
            AppendReading(builder.Append(" A "), ARank(mountIndex));
            AppendReading(builder.Append(", S "), SRank(mountIndex));
        }

        return builder.ToString();
    }

    private static void AppendReading(StringBuilder builder, CounterReading reading)
    {
        if (reading.Status == CounterStatus.Unknown)
        {
            builder.Append("unknown");
            return;
        }

        builder.Append(reading.Current).Append('/').Append(reading.Needed);
        if (reading.Status == CounterStatus.Complete)
        {
            builder.Append(" complete");
        }
    }
}
