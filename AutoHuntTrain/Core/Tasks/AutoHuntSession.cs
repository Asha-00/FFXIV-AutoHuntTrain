using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Train;
using ECommons.DalamudServices;
using System.Threading;

namespace AutoHuntTrain.Core.Tasks;

// One run of the controller: the clock, the job, the marks the kill ledger credited, what the wallet gained, and what
// the ride learned that has to outlive a pause, since every resume builds the ride task afresh.
public sealed class AutoHuntSession
{
    // Set once an announced ride stands at the train's start, so a resume does not travel there again.
    internal bool ArrivedAtTrain;

    internal ConductorIdentity Conductor = ConductorIdentity.None;

    internal ConductorSource ConductorSource;

    // Set on a ride rebuilt at login, so its first journey waits on the transfer in flight instead of asking again.
    internal bool ResumeJourney;

    internal bool CrossedDataCenter;

    // Set from the moment a data center transfer is cleared to start until the journey is over; the game would refuse
    // the transfer with a party joined meanwhile.
    internal bool DataCenterTransferPending;

    // Whether the character was in a party when the ride started: that party is the player's own and is not left.
    internal bool InPartyAtStart;

    internal bool LookingForGroupPosted;

    // When the last flag the ride finished with was posted, so a resume does not follow that flag a second time.
    internal DateTime LastSettledFlagAtUtc = DateTime.MinValue;

    // Null until the ride decides how it ended; a ride cut by the relog of a transfer never decides.
    internal RideOutcome? Outcome;

    private HuntWallet lastWallet;
    private bool walletKnown;

    private long pausedMs;
    private long pauseStartedAtMs;
    private DateTime? endedAt;

    public AutoHuntSession()
    {
        JobAbbreviation = CurrentJobAbbreviation();
        Rebaseline();
    }

    public DateTime StartedAt { get; private set; } = DateTime.UtcNow;

    public string JobAbbreviation { get; private set; }

    // The source announcement's kind lets shared running and history views avoid train-only labels during an S-Hunt.
    // Train is the enum's zero value, so manual conductor rides and sessions created by older code remain trains.
    internal AnnouncementKind Kind { get; set; } = AnnouncementKind.Train;

    public bool IsSRank => Kind == AnnouncementKind.SRank;

    // Filled in by the ride once one exists.
    public string WorldName { get; internal set; } = string.Empty;

    public string DataCenterName { get; internal set; } = string.Empty;

    public ExpansionKind? Expansion { get; internal set; }

    public ExpansionGroup? Group { get; internal set; }

    public int MarksCredited { get; private set; }

    public int AlliedSeals { get; private set; }

    public int CenturioSeals { get; private set; }

    public int Nuts { get; private set; }

    public bool EndedWithFault { get; private set; }

    public bool CompletedByStopCondition;

    internal bool Recorded;
    internal bool AfterActionDispatched;

    public bool DidNothing => MarksCredited == 0 && AlliedSeals == 0 && CenturioSeals == 0 && Nuts == 0;

    public int Seals => AlliedSeals + CenturioSeals;

    public TimeSpan Elapsed => (endedAt ?? DateTime.UtcNow) - StartedAt - TimeSpan.FromMilliseconds(PausedTotalMs);

    public double MarksPerHour => Elapsed.TotalHours > 0 ? MarksCredited / Elapsed.TotalHours : 0;

    private long PausedTotalMs
        => pausedMs + (pauseStartedAtMs == 0 ? 0 : Environment.TickCount64 - pauseStartedAtMs);

    internal void CreditMark() => MarksCredited++;

    // A ride rebuilt after the relog is the same ride: it keeps the clock and the credits from before the transfer.
    internal void Restore(DateTime startedAtUtc, int marksCredited)
    {
        StartedAt = startedAtUtc;
        MarksCredited = marksCredited;
    }

    public void Sample()
    {
        SampleWallet(credit: true);
        if (JobAbbreviation.Length == 0)
        {
            JobAbbreviation = CurrentJobAbbreviation();
        }
    }

    // Makes the current wallet the new zero point, so seals earned while the run was paused are not credited to it.
    public void Rebaseline() => SampleWallet(credit: false);

    // A Stop unwinds the run loop through the same catch as a genuine fault, and only a genuine fault may resume the run.
    public void RecordFault(Exception exception, CancellationToken cancelToken)
    {
        if (cancelToken.IsCancellationRequested || exception is OperationCanceledException)
        {
            return;
        }

        EndedWithFault = true;
        // clib's task runner writes the same exception to dalamud.log when the task unwinds.
        RunLog.Record(RunLogLevel.Error, exception, "The ride task ended with an unexpected error");
    }

    internal void ClearFault() => EndedWithFault = false;

    public void BeginPause()
    {
        if (pauseStartedAtMs != 0)
        {
            return;
        }

        pauseStartedAtMs = Environment.TickCount64;
    }

    public void EndPause()
    {
        if (pauseStartedAtMs == 0)
        {
            return;
        }

        pausedMs += Environment.TickCount64 - pauseStartedAtMs;
        pauseStartedAtMs = 0;
    }

    // Freezes the clock, so a finished run left on screen during its after-run action stops counting.
    internal void End()
    {
        if (endedAt is not null)
        {
            return;
        }

        EndPause();
        endedAt = DateTime.UtcNow;
    }

    private void SampleWallet(bool credit)
    {
        if (!HuntWallet.TryRead(out var wallet))
        {
            return;
        }

        if (credit && walletKnown)
        {
            AlliedSeals += CurrencyGained(lastWallet.AlliedSeals, wallet.AlliedSeals);
            CenturioSeals += CurrencyGained(lastWallet.CenturioSeals, wallet.CenturioSeals);
            Nuts += CurrencyGained(lastWallet.Nuts, wallet.Nuts);
        }

        lastWallet = wallet;
        walletKnown = true;
    }

    // Only rises count, because a spend between two samples would otherwise cancel out currency the run earned.
    private static int CurrencyGained(int before, int after) => after > before ? after - before : 0;

    private static string CurrentJobAbbreviation()
        => Svc.Objects.LocalPlayer?.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? string.Empty;
}
