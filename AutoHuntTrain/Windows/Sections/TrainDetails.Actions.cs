using AutoHuntTrain.Core;
using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Game;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Travel;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// The announcement's actions, flowing onto a new line when the card is narrow: Ride or S-Hunt with the same rules and
// refusal as the list, the map flag, the Party Finder, the on-screen arrow, and a relay to chat with a picker for a
// one-off channel.
internal sealed partial class TrainDetails
{
    private const float ActionHeight = 30f;
    private const float ActionGap = 8f;
    private const float PickerGap = 2f;
    private const float CatchUpNoteGap = 8f;
    private const int VerdictRefreshMs = 250;
    private const string RideId = "##aht_details_ride";
    private const string FlagId = "##aht_details_flag";
    private const string PartyFinderId = "##aht_details_pf";
    private const string NavId = "##aht_details_nav";
    private const string RelayId = "##aht_details_relay";
    private const string RelayPickerId = "##aht_details_relay_pick";
    private const string RelayMenuId = "##aht_details_relay_menu";

    private RideVerdict verdict;
    private bool catchesUp;
    private int verdictForId = -1;
    private long verdictAtMs;
    private CachedText relayHint;

    private float DrawActions(Plugin plugin, in Announcement announcement, float left, float right, float y)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var height = ActionHeight * scale;
        var gap = ActionGap * scale;
        var cursor = new Vector2(left, y);
        RefreshVerdict(announcement);

        var rideable = verdict == RideVerdict.Rideable;
        var rideLabel = Loc.T(announcement.IsSRank ? L.Feed.SHunt : L.Feed.Ride);
        var rideIcon = announcement.IsSRank ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Train;
        var rideWidth = PillButton.Width(rideLabel, rideIcon);
        var rideOrigin = Place(ref cursor, rideWidth, left, right, height, gap);
        if (PillButton.Draw(RideId, rideLabel, Styling.AccentMint, rideable ? PillButton.Emphasis.Filled : PillButton.Emphasis.Ghost,
            rideIcon, rideable, ActionHeight, rideable ? TrainTexts.RideHint(announcement, catchesUp) : null))
        {
            plugin.Controller.StartAnnouncement(announcement);
        }
        else if (!rideable && Hit.HoveringRect(rideOrigin, rideOrigin + new Vector2(rideWidth, height)))
        {
            Tooltip.Show(TrainTexts.Verdict(verdict));
        }

        if (hasFlagPoint)
        {
            var flagLabel = Loc.T(L.Details.Flag);
            Place(ref cursor, PillButton.Width(flagLabel, FontAwesomeIcon.MapMarkerAlt), left, right, height, gap);
            var flagHint = Loc.T(announcement.IsSRank ? L.Details.FlagHintSRank : L.Details.FlagHint);
            if (PillButton.Draw(FlagId, flagLabel, Styling.AccentBlue, PillButton.Emphasis.Tinted, FontAwesomeIcon.MapMarkerAlt, height: ActionHeight, tooltip: flagHint)
                && !MapFlag.TryOpenMap(announcement.TerritoryId, flagPoint))
            {
                RunLog.Warning($"Details: the map could not be opened on the announced location in {TerritoryNames.Of(announcement.TerritoryId)}");
            }
        }

        var partyFinderLabel = Loc.T(L.Details.PartyFinder);
        Place(ref cursor, PillButton.Width(partyFinderLabel, FontAwesomeIcon.Users), left, right, height, gap);
        if (PillButton.Draw(PartyFinderId, partyFinderLabel, Styling.AccentAmber, PillButton.Emphasis.Tinted, FontAwesomeIcon.Users, height: ActionHeight, tooltip: Loc.T(L.Details.PartyFinderHint)))
        {
            plugin.PartyFinder.Request();
        }

        if (hasFlagPoint)
        {
            DrawNav(announcement, ref cursor, left, right, height, gap);
        }

        DrawRelay(plugin.Configuration, announcement, ref cursor, left, right, height, gap);
        return DrawCatchUpNote(left, right, cursor.Y + height);
    }

    private float DrawCatchUpNote(float left, float right, float y)
    {
        if (!catchesUp || verdict != RideVerdict.Rideable)
        {
            return y;
        }

        using (Fonts.PushCaption())
        {
            var noteY = y + CatchUpNoteGap * ImGuiHelpers.GlobalScale;
            TextDraw.At(TextDraw.Truncate(Loc.T(L.Details.CatchUpNote), right - left), new Vector2(left, noteY), Styling.AccentAmberSoft);
            return noteY + ImGui.GetTextLineHeight();
        }
    }

    private void DrawNav(in Announcement announcement, ref Vector2 cursor, float left, float right, float height, float gap)
    {
        var active = NavWaypoint.IsFor(announcement.Id);
        var label = Loc.T(active ? L.Details.NavOn : L.Details.Nav);
        Place(ref cursor, PillButton.Width(label, FontAwesomeIcon.LocationArrow), left, right, height, gap);
        var activeHint = announcement.IsSRank ? navHint : Loc.T(L.Details.NavOnHint);
        var clicked = PillButton.Draw(NavId, label, Styling.AccentGlow, active ? PillButton.Emphasis.Filled : PillButton.Emphasis.Tinted,
            FontAwesomeIcon.LocationArrow, height: ActionHeight, tooltip: active ? activeHint : navHint);
        if (!clicked)
        {
            return;
        }

        if (active)
        {
            NavWaypoint.Clear();
            return;
        }

        if (!NavWaypoint.TrySet(announcement.Id, announcement.TerritoryId, announcement.World.Id, flagPoint))
        {
            RunLog.Warning($"Details: no arrow, the map spot in {TerritoryNames.Of(announcement.TerritoryId)} does not resolve to a place in the world");
            return;
        }

        var there = Svc.ClientState.TerritoryType == announcement.TerritoryId && Worlds.TryCurrent(out var world) && world.Id == announcement.World.Id;
        var message = there ? Loc.T(L.Details.NavSetHere) : Loc.T(L.Details.NavSet, TrainFacts.StartZone(announcement), announcement.World.Name);
        Svc.Chat.Print($"{AhtConstants.LogPrefix} {message}");
    }

    private void DrawRelay(Configuration configuration, in Announcement announcement, ref Vector2 cursor, float left, float right, float height, float gap)
    {
        var label = Loc.T(L.Details.Relay);
        var relayWidth = PillButton.Width(label, FontAwesomeIcon.Bullhorn);
        var pickerSize = height;
        Place(ref cursor, relayWidth + PickerGap * ImGuiHelpers.GlobalScale + pickerSize, left, right, height, gap);
        var origin = ImGui.GetCursorScreenPos();
        if (PillButton.Draw(RelayId, label, Styling.AccentNebula, PillButton.Emphasis.Tinted, FontAwesomeIcon.Bullhorn, height: ActionHeight,
            tooltip: RelayHint(announcement, configuration.RelayChannel)))
        {
            TrainRelay.Send(announcement, configuration.RelayChannel, configuration.RelayWithFlag);
        }

        ImGui.SetCursorScreenPos(new Vector2(origin.X + relayWidth + PickerGap * ImGuiHelpers.GlobalScale, origin.Y));
        if (IconButton.Draw(FontAwesomeIcon.ChevronDown, RelayPickerId, pickerSize, Styling.TextDim, Loc.T(L.Details.RelayPick)))
        {
            ImGui.OpenPopup(RelayMenuId);
        }

        using var menu = ContextMenu.Begin(RelayMenuId);
        if (!menu.Open)
        {
            return;
        }

        var labels = RelayChannelLabels.All();
        for (var index = 0; index < labels.Length; index++)
        {
            if (ImGui.MenuItem(labels[index]))
            {
                TrainRelay.Send(announcement, (RelayChannel)index, configuration.RelayWithFlag);
            }
        }
    }

    // Moves the cursor to where an action of this width goes, starting a new line when it would pass the right edge.
    private static Vector2 Place(ref Vector2 cursor, float width, float left, float right, float height, float gap)
    {
        if (cursor.X > left && cursor.X + width > right)
        {
            cursor = new Vector2(left, cursor.Y + height + gap);
        }

        var origin = cursor;
        ImGui.SetCursorScreenPos(origin);
        cursor = new Vector2(cursor.X + width + gap, cursor.Y);
        return origin;
    }

    private void RefreshVerdict(in Announcement announcement)
    {
        var now = Environment.TickCount64;
        if (verdictForId == announcement.Id && now - verdictAtMs < VerdictRefreshMs)
        {
            return;
        }

        verdictForId = announcement.Id;
        verdictAtMs = now;
        verdict = RideRules.EvaluateManual(announcement, out _);
        catchesUp = TrainTexts.CatchesUp(announcement, DateTime.UtcNow);
    }

    private string RelayHint(in Announcement announcement, RelayChannel channel)
    {
        var key = HashCode.Combine(announcement.IsSRank, channel);
        if (relayHint.TryGet(key, out var text))
        {
            return text;
        }

        var hint = announcement.IsSRank ? L.Details.RelayHintSRank : L.Details.RelayHint;
        return relayHint.Set(key, Loc.T(hint, RelayChannelLabels.Of(channel)));
    }
}
