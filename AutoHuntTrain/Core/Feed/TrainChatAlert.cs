using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.ChatMethods;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI;

namespace AutoHuntTrain.Core.Feed;

// One chat line for each announced train the Upcoming list would show, with a link to its details, so HuntAlerts'
// own chat alert can be switched off. It keeps quiet when the other notifications do, and follows the list's view
// rather than the ride rules, so the chat and the list agree.
internal sealed class TrainChatAlert : IDisposable
{
    public const int MinimumSoundEffect = 1;
    public const int MaximumSoundEffect = 16;

    private readonly FeedListener feed;
    private readonly Action<Announcement> onAnnounced;

    public TrainChatAlert(FeedListener feed)
    {
        this.feed = feed;
        onAnnounced = OnAnnounced;
        feed.Announced += onAnnounced;
    }

    public void Dispose() => feed.Announced -= onAnnounced;

    public static unsafe void PlaySound(int effect)
        => UIGlobals.PlayChatSoundEffect((uint)Math.Clamp(effect, MinimumSoundEffect, MaximumSoundEffect));

    private void OnAnnounced(Announcement announcement)
    {
        // The existing chat-alert preference describes announced trains. Do not duplicate HuntAlerts' S-rank chat or
        // imply that the one-off hunt will auto-start; S ranks remain visible and manually actionable in the feed.
        if (announcement.IsSRank)
        {
            RunLog.Debug($"Chat alert: S rank on {announcement.World.Name} is not posted by the train chat alert");
            return;
        }

        var configuration = Plugin.Instance.Configuration;
        if (!configuration.ChatAlert)
        {
            return;
        }

        var group = ExpansionGroups.Name(announcement.Group);
        if (NotifyGates.TryBlock(configuration, out var reason))
        {
            RunLog.Debug($"Chat alert: the {group} train on {announcement.World.Name} is not posted, {reason}");
            return;
        }

        if (!TrainListFilter.Shows(announcement, configuration.TrainListView))
        {
            RunLog.Debug($"Chat alert: the {group} train on {announcement.World.Name} is outside the list's view");
            return;
        }

        Svc.Chat.Print(BuildLine(announcement));
        if (configuration.ChatAlertSound)
        {
            PlaySound(configuration.ChatAlertSoundEffect);
        }
    }

    private static SeString BuildLine(in Announcement announcement)
    {
        var world = announcement.World;
        var countdown = TrainFacts.CountdownText(TrainFacts.WholeMinutesToStart(announcement, DateTime.UtcNow));
        var line = Loc.T(L.Notify.ChatLine, ExpansionGroups.LocalName(announcement.Group), world.Name, world.DataCenterName, RegionLabels.Name(world.Region), countdown);
        if (announcement.NamesConductor)
        {
            line = string.Concat(line, Loc.T(L.Notify.ChatConductor, Conductor.Describe(announcement.Conductor)));
        }

        return new SeStringBuilder()
            .AddText($"{AhtConstants.LogPrefix} {line} ")
            .Add(Plugin.Instance.TrainDetailsLink(announcement.Id))
            .AddUiForeground($"[{Loc.T(L.Notify.ChatDetails)}]", (ushort)UIColor.LightBlue)
            .Add(RawPayload.LinkTerminator)
            .Build();
    }
}
