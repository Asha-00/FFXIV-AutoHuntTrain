using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// The HuntAlerts announcements the picked view shows: expansion, region when it is not the player's, world, event
// status, reachability and a manual action. Trains keep their countdown, conductor and Ride action; S ranks show their
// report age, exact creature and S-Hunt action. A click anywhere else on a row opens its details. Verdicts are refreshed
// a few times a second rather than every frame, because each one asks Lifestream whether it is busy.
internal static class FeedCard
{
    public const int NoTrain = 0;

    private const float PadX = 18f;
    private const float PadY = 12f;
    private const float RowHeight = 54f;
    private const float RowInsetX = 8f;
    private const float RowRounding = 10f;
    private const float LineGap = 3f;
    private const float BadgeGap = 10f;
    private const float ChipGap = 8f;
    private const float ButtonGap = 14f;
    private const float RideButtonHeight = 26f;
    private const int VerdictRefreshMs = 250;
    private const string RideButtonId = "##aht_feed_ride";
    private const string RowId = "##aht_feed_row";

    private static readonly CachedText[] worldLines = new CachedText[FeedListener.Capacity];
    private static readonly CachedText[] countdowns = new CachedText[FeedListener.Capacity];
    private static readonly CachedText[] conductorLines = new CachedText[FeedListener.Capacity];
    private static readonly CachedText[] creatureLines = new CachedText[FeedListener.Capacity];
    private static readonly RideVerdict[] verdicts = new RideVerdict[FeedListener.Capacity];
    private static readonly Reachability[] reachabilities = new Reachability[FeedListener.Capacity];
    private static readonly bool[] listed = new bool[FeedListener.Capacity];
    private static readonly bool[] autoJoined = new bool[FeedListener.Capacity];
    private static readonly bool[] homeRegion = new bool[FeedListener.Capacity];
    private static readonly bool[] catchesUp = new bool[FeedListener.Capacity];

    private static int listedCount;

    private static long verdictsRefreshedAtMs;
    private static int verdictsVersion = -1;
    private static TrainListView verdictsView;

    // The id of the announcement whose row was clicked, or NoTrain.
    public static int Draw(Plugin plugin)
    {
        var feed = plugin.Feed;
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var width = ImGui.GetContentRegionAvail().X;
        var padX = PadX * scale;
        var innerWidth = width - padX * 2f;
        var x = origin.X + padX;
        var y = origin.Y + PadY * scale;
        var nowUtc = DateTime.UtcNow;
        RefreshVerdicts(feed, nowUtc);

        drawList.ChannelsSplit(2);
        drawList.ChannelsSetCurrent(1);
        var drawn = 0;
        var opened = NoTrain;
        for (var index = 0; index < feed.Count; index++)
        {
            if (!listed[index])
            {
                continue;
            }

            if (drawn++ > 0)
            {
                Paint.Hairline(drawList, new Vector2(x, y), new Vector2(x + innerWidth, y));
            }

            y = DrawRow(plugin, feed[index], index, nowUtc, x, y, innerWidth, drawList, ref opened);
        }

        var end = new Vector2(origin.X + width, y + PadY * scale);
        drawList.ChannelsSetCurrent(0);
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);
        drawList.ChannelsMerge();

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(width, end.Y - origin.Y));
        return opened;
    }

    // How many announcements the picked view shows, so the page can show its empty state instead of an empty card.
    public static int ListedCount(FeedListener feed)
    {
        RefreshVerdicts(feed, DateTime.UtcNow);
        return listedCount;
    }

    public static string ReachLabel(Reachability reachability) => reachability switch
    {
        Reachability.SameWorld => Loc.T(L.Feed.ReachSameWorld),
        Reachability.SameDataCenter => Loc.T(L.Feed.ReachSameDataCenter),
        Reachability.CrossDataCenter => Loc.T(L.Feed.ReachCrossDataCenter),
        _ => Loc.T(L.Feed.ReachOutOfRegion),
    };

    public static Vector4 ReachColor(Reachability reachability) => reachability switch
    {
        Reachability.SameWorld => Styling.AccentMint,
        Reachability.SameDataCenter => Styling.AccentBlue,
        Reachability.CrossDataCenter => Styling.AccentAmber,
        _ => Styling.AccentRose,
    };

    private static void RefreshVerdicts(FeedListener feed, DateTime nowUtc)
    {
        var now = Environment.TickCount64;
        var view = Plugin.Instance.Configuration.TrainListView;
        if (feed.Version == verdictsVersion && view == verdictsView && now - verdictsRefreshedAtMs < VerdictRefreshMs)
        {
            return;
        }

        verdictsVersion = feed.Version;
        verdictsView = view;
        verdictsRefreshedAtMs = now;
        listedCount = 0;
        var controller = Plugin.Instance.Controller;
        for (var index = 0; index < feed.Count; index++)
        {
            verdicts[index] = RideRules.EvaluateManual(feed[index], out reachabilities[index]);
            autoJoined[index] = feed[index].IsTrain
                && verdicts[index] == RideVerdict.Rideable
                && !controller.AutoJoinSkips(feed[index].Id)
                && RideRules.EvaluateAutoRules(feed[index], nowUtc, reachabilities[index]) == RideVerdict.Rideable;
            listed[index] = TrainListFilter.Shows(feed[index], view);
            homeRegion[index] = TrainListFilter.InHomeRegion(feed[index].World);
            catchesUp[index] = TrainTexts.CatchesUp(feed[index], nowUtc);
            if (listed[index])
            {
                listedCount++;
            }
        }
    }

    private static float DrawRow(Plugin plugin, Announcement announcement, int index, DateTime nowUtc, float x, float y, float width, ImDrawListPtr drawList, ref int opened)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var rowHeight = RowHeight * scale;
        var lineHeight = ImGui.GetTextLineHeight();
        float captionHeight;
        using (Fonts.PushCaption())
        {
            captionHeight = ImGui.GetTextLineHeight();
        }

        var top = y + (rowHeight - lineHeight - LineGap * scale - captionHeight) * 0.5f;
        var verdict = verdicts[index];
        var rideable = verdict == RideVerdict.Rideable;
        var label = Loc.T(announcement.IsSRank ? L.Feed.SHunt : L.Feed.Ride);
        var icon = announcement.IsSRank ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Train;
        var buttonWidth = PillButton.Width(label, icon);
        var buttonHeight = RideButtonHeight * scale;
        var buttonOrigin = new Vector2(x + width - buttonWidth, y + (rowHeight - buttonHeight) * 0.5f);
        var textRight = buttonOrigin.X - ButtonGap * scale;

        ImGui.PushID(announcement.Id);
        if (DrawRowHit(drawList, x, y, textRight + ButtonGap * scale * 0.5f, rowHeight))
        {
            opened = announcement.Id;
        }

        ImGui.SetCursorScreenPos(buttonOrigin);
        var clicked = PillButton.Draw(RideButtonId, label, Styling.AccentMint, rideable ? PillButton.Emphasis.Filled : PillButton.Emphasis.Ghost,
            icon, rideable, RideButtonHeight, rideable ? TrainTexts.RideHint(announcement, catchesUp[index]) : null);
        ImGui.PopID();
        if (clicked)
        {
            plugin.Controller.StartAnnouncement(announcement);
        }
        else if (!rideable && Hit.HoveringRect(buttonOrigin, buttonOrigin + new Vector2(buttonWidth, buttonHeight)))
        {
            Tooltip.Show(TrainTexts.Verdict(verdict));
        }

        var badgeMidY = top + lineHeight * 0.5f;
        var textX = x + Badge.DrawLeft(drawList, GroupLabels.Name(announcement.Group), GroupLabels.Color(announcement.Group), x, badgeMidY) + BadgeGap * scale;
        if (!homeRegion[index])
        {
            textX += Badge.DrawLeft(drawList, RegionLabels.Name(announcement.World.Region), Styling.AccentRose, textX, badgeMidY) + BadgeGap * scale;
        }

        TextDraw.At(TextDraw.Truncate(WorldLine(index, announcement), textRight - textX), new Vector2(textX, top), Styling.TextStrong);

        var captionY = top + lineHeight + LineGap * scale;
        using (Fonts.PushCaption())
        {
            var cursorX = textX;
            var status = TrainTexts.Status(ref countdowns[index], announcement, nowUtc, out var color);
            TextDraw.At(status, new Vector2(cursorX, captionY), color);
            cursorX += TextDraw.Measure(status).X + ChipGap * scale;
            if (announcement.IsTrain && announcement.InProgressAt(nowUtc))
            {
                cursorX += Badge.DrawLeft(drawList, Loc.T(L.Feed.InProgress), Styling.AccentAmber, cursorX, captionY + captionHeight * 0.5f) + ChipGap * scale;
            }

            var reachability = reachabilities[index];
            cursorX += Badge.DrawLeft(drawList, ReachLabel(reachability), ReachColor(reachability), cursorX, captionY + captionHeight * 0.5f) + ChipGap * scale;
            if (autoJoined[index])
            {
                cursorX += Badge.DrawLeft(drawList, Loc.T(L.Feed.AutoMarker), Styling.AccentMint, cursorX, captionY + captionHeight * 0.5f) + ChipGap * scale;
            }

            if (cursorX < textRight)
            {
                var detail = announcement.IsSRank
                    ? CreatureText(index, announcement)
                    : announcement.NamesConductor
                        ? ConductorText(index, announcement)
                        : string.Empty;
                if (detail.Length > 0)
                {
                    TextDraw.At(TextDraw.Truncate(detail, textRight - cursorX), new Vector2(cursorX, captionY), Styling.TextDim);
                }
            }
        }

        ImGui.SetCursorScreenPos(new Vector2(x, y));
        ImGui.Dummy(new Vector2(width, rowHeight));
        return y + rowHeight;
    }

    // The row up to its action button, lit while hovered; drawn before the row's text so the light sits behind it.
    private static bool DrawRowHit(ImDrawListPtr drawList, float x, float y, float right, float rowHeight)
    {
        var inset = RowInsetX * ImGuiHelpers.GlobalScale;
        var min = new Vector2(x - inset, y);
        var max = new Vector2(right, y + rowHeight);
        ImGui.SetCursorScreenPos(min);
        var hit = Hit.Area(RowId, max - min);
        var hover = Motion.Hover(Motion.Key(RowId), hit.Hovered);
        if (hover > 0.01f)
        {
            Paint.Fill(drawList, min, max, Styling.WithAlpha(Styling.Surface2, 0.7f * hover), RowRounding * ImGuiHelpers.GlobalScale);
        }

        return hit.Clicked;
    }

    private static string WorldLine(int index, in Announcement announcement)
    {
        if (worldLines[index].TryGet(announcement.Id, out var line))
        {
            return line;
        }

        return worldLines[index].Set(announcement.Id, Loc.T(L.Feed.WorldLine, announcement.World.Name, announcement.World.DataCenterName));
    }

    private static string ConductorText(int index, in Announcement announcement)
    {
        if (conductorLines[index].TryGet(announcement.Id, out var line))
        {
            return line;
        }

        return conductorLines[index].Set(announcement.Id, Loc.T(L.Feed.ConductorNamed, Conductor.Describe(announcement.Conductor)));
    }

    private static string CreatureText(int index, in Announcement announcement)
    {
        if (creatureLines[index].TryGet(announcement.Id, out var line))
        {
            return line;
        }

        if (announcement.CreatureNameId is not > 0)
        {
            return creatureLines[index].Set(announcement.Id, string.Empty);
        }

        var creature = HuntMarkRegistry.NameOf(announcement.CreatureNameId.Value);
        var zone = announcement.NamesTerritory ? TerritoryNames.Of(announcement.TerritoryId) : string.Empty;
        var instance = announcement.NamesInstance ? $" i{announcement.Instance}" : string.Empty;
        var detail = zone.Length > 0
            ? $"{creature} · {zone}{instance}"
            : $"{creature}{instance}";
        return creatureLines[index].Set(announcement.Id, detail);
    }
}
