using AutoHuntTrain.Core.Game;

namespace AutoHuntTrain.Core.Feed;

// Tells a player who is not looking at the plugin that a train they could ride was announced: the window opens on
// the Train page and the taskbar flashes while the game is in the background. The chat line is TrainChatAlert's.
internal sealed class TrainNotifier : IDisposable
{
    private const int RateLimitMs = 20_000;
    private const int OpenWindowPatienceMs = 30_000;

    private readonly FeedListener feed;
    private readonly Action<Announcement> onAnnounced;
    private long nextNotifyAtMs;
    private long openWindowDeadlineMs;
    private bool openWindowPending;

    public TrainNotifier(FeedListener feed)
    {
        this.feed = feed;
        onAnnounced = OnAnnounced;
        feed.Announced += onAnnounced;
    }

    public void Dispose() => feed.Announced -= onAnnounced;

    // The window never takes the keyboard from a text box; it waits for the player to finish typing, within reason.
    public void Tick()
    {
        if (!openWindowPending)
        {
            return;
        }

        if (Environment.TickCount64 > openWindowDeadlineMs)
        {
            openWindowPending = false;
            RunLog.Debug("Notify: a text box kept the keyboard for 30 seconds; the Train page is not opened");
            return;
        }

        if (TextInputFocus.Active())
        {
            return;
        }

        openWindowPending = false;
        Plugin.Instance.ShowTrainPage();
    }

    public void Test()
    {
        var configuration = Plugin.Instance.Configuration;
        RunLog.Info("Notify: testing the notifications");
        if (configuration.NotifyOpenWindow)
        {
            RequestOpenWindow();
        }

        if (configuration.NotifyFlashTaskbar)
        {
            WindowFlash.FlashBriefly();
        }
    }

    private void OnAnnounced(Announcement announcement)
    {
        // S ranks have their own urgency and manual pull-safety semantics. Until they receive dedicated notification
        // settings, keep the existing train notification preferences train-only.
        if (announcement.IsSRank)
        {
            RunLog.Debug($"Notify: S rank on {announcement.World.Name} is not notified by the train notifier");
            return;
        }

        var configuration = Plugin.Instance.Configuration;
        if (!configuration.NotifyOpenWindow && !configuration.NotifyFlashTaskbar)
        {
            return;
        }

        var group = ExpansionGroups.Name(announcement.Group);
        if (!ShouldNotify(configuration, announcement, out var reason))
        {
            RunLog.Debug($"Notify: the {group} train on {announcement.World.Name} is not notified, {reason}");
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextNotifyAtMs)
        {
            RunLog.Debug($"Notify: the {group} train on {announcement.World.Name} is not notified, another notification went out less than 20 seconds ago");
            return;
        }

        nextNotifyAtMs = now + RateLimitMs;
        RunLog.Info($"Notify: the {group} train on {announcement.World.Name} could be ridden");
        if (configuration.NotifyOpenWindow)
        {
            RequestOpenWindow();
        }

        if (configuration.NotifyFlashTaskbar && WindowFlash.GameInBackground())
        {
            WindowFlash.FlashUntilFocused();
        }
    }

    // A train on an allowed data center that a Ride click would take, whatever the auto-join switches say. Lifestream
    // being busy is a refusal the player can lift on the spot, so it still notifies.
    private static bool ShouldNotify(Configuration configuration, in Announcement announcement, out string reason)
    {
        if (NotifyGates.TryBlock(configuration, out reason))
        {
            return false;
        }

        if (!RideRules.IsAllowedDataCenter(announcement.World))
        {
            reason = RideRules.Explain(RideVerdict.NotAllowedDataCenter);
            return false;
        }

        var verdict = RideRules.EvaluateManual(announcement, out _);
        if (verdict is not (RideVerdict.Rideable or RideVerdict.LifestreamBusy))
        {
            reason = RideRules.Explain(verdict);
            return false;
        }

        reason = string.Empty;
        return true;
    }

    private void RequestOpenWindow()
    {
        openWindowPending = true;
        openWindowDeadlineMs = Environment.TickCount64 + OpenWindowPatienceMs;
    }
}
