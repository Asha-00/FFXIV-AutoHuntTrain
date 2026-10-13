using clib.Services;

namespace AutoHuntTrain.Core.Tasks;

internal enum PauseReason { None, Manual, InContent }

internal sealed partial class AutoHuntController
{
    public PauseReason PauseReason { get; private set; } = PauseReason.None;

    public bool Paused => PauseReason != PauseReason.None;

    public bool CanPause => session is not null && Phase is not (HuntPhase.Idle or HuntPhase.Paused or HuntPhase.Finishing);

    public void Pause(PauseReason reason)
    {
        if (reason == PauseReason.None)
        {
            return;
        }

        if (Paused)
        {
            if (reason == PauseReason.Manual && PauseReason == PauseReason.InContent)
            {
                PauseReason = PauseReason.Manual;
                Diag("Auto-pause promoted to a manual pause; leaving content will no longer resume.");
            }

            return;
        }

        if (!CanPause)
        {
            if (reason == PauseReason.Manual)
            {
                ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Nothing to pause.");
            }

            return;
        }

        var pausing = session!;
        PauseReason = reason;
        progress.SetPhase(HuntPhase.Paused);
        // Resume re-baselines the session, so progress up to this moment has to be credited now.
        pausing.Sample();
        pausing.BeginPause();
        currentTask = null;
        Svc.Automation.Stop();
        ReleaseHelpers();
        Plugin.Instance.PartyFinderListing.Withdraw("the ride was paused");

        Diag($"Run paused ({reason}); session kept at {pausing.MarksCredited} marks credited.");
        ECommons.DalamudServices.Svc.Chat.Print(reason == PauseReason.InContent
            ? $"{AhtConstants.LogPrefix} Paused: you are in instanced content. The ride resumes once you are back outside."
            : $"{AhtConstants.LogPrefix} Paused. Your ride and session stats are kept until you resume or stop.");
    }

    public void Resume()
    {
        if (!Paused)
        {
            return;
        }

        var resuming = session;
        if (resuming is null || !CanRestart())
        {
            Diag("Resume requested with no session or nothing to resume; stopping instead.");
            Stop();
            return;
        }

        PauseReason = PauseReason.None;
        resuming.EndPause();
        resuming.Rebaseline();
        Diag("Resuming the ride.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Resuming the ride.");
        RunRide(resuming);
    }

    public void TogglePause()
    {
        if (Paused)
        {
            Resume();
            return;
        }

        Pause(PauseReason.Manual);
    }
}
