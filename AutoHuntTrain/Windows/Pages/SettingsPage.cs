using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;
using AutoHuntTrain.Windows.Sections.Config;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoHuntTrain.Windows.Pages;

internal sealed class SettingsPage
{
    private enum Tab { General, Feed, Ride, Humanizer, Upkeep, Party, GmAlert }

    private readonly record struct Entry(Tab Tab, LocString Label, FontAwesomeIcon Icon, LocString Subtitle);

    // Ordered like Tab, because the active tab indexes this array.
    private static readonly Entry[] entries =
    [
        new(Tab.General, L.Settings.CatGeneral, FontAwesomeIcon.Cog, L.Settings.CatGeneralSub),
        new(Tab.Feed, L.Feed.SettingsGroup, FontAwesomeIcon.Rss, L.Settings.CatFeedSub),
        new(Tab.Ride, L.Train.Ride, FontAwesomeIcon.Train, L.Settings.CatRideSub),
        new(Tab.Humanizer, L.Humanizer.Tab, FontAwesomeIcon.UserClock, L.Humanizer.TabSub),
        new(Tab.Upkeep, L.Safety.CatUpkeep, FontAwesomeIcon.Wrench, L.Safety.CatUpkeepSub),
        new(Tab.Party, L.Safety.CatParty, FontAwesomeIcon.Users, L.Safety.CatPartySub),
        new(Tab.GmAlert, L.Safety.CatGmAlert, FontAwesomeIcon.UserSecret, L.Safety.CatGmAlertSub),
    ];

    private Tab activeTab = Tab.General;
    private bool resetScroll;

    public void Draw(Plugin plugin)
    {
        var configuration = plugin.Configuration;
        var scale = ImGuiHelpers.GlobalScale;
        var navWidth = Layout.SettingsNavWidth * scale;

        using (ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, Vector2.Zero))
        {
            using (var nav = ImRaii.Child("##aht_settings_nav", new Vector2(navWidth, -1f), false, ImGuiWindowFlags.NoScrollbar))
            {
                if (nav)
                {
                    DrawNav();
                }
            }

            ImGui.SameLine(0f, 18f * scale);

            using (var content = ImRaii.Child("##aht_settings_content", new Vector2(-1f, -1f), false, ImGuiWindowFlags.None))
            {
                if (content)
                {
                    DrawContent(configuration);
                }
            }
        }
    }

    private void DrawNav()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var title = Loc.T(L.Settings.Title);
        using (Fonts.PushTitle())
        {
            TextDraw.At(title, new Vector2(origin.X + 6f * scale, origin.Y), Styling.TextStrong);
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, TextDraw.Measure(title).Y + 10f * scale));
        }

        for (var index = 0; index < entries.Length; index++)
        {
            var entry = entries[index];
            if (SidebarTab.Draw(Loc.T(entry.Label), entry.Icon, Styling.AccentGlow, activeTab == entry.Tab))
            {
                Select(entry.Tab);
            }
        }
    }

    private void Select(Tab tab)
    {
        if (activeTab == tab)
        {
            return;
        }

        activeTab = tab;
        resetScroll = true;
    }

    private void DrawContent(Configuration configuration)
    {
        if (resetScroll)
        {
            ImGui.SetScrollY(0f);
            resetScroll = false;
        }

        var entry = entries[(int)activeTab];
        var scale = ImGuiHelpers.GlobalScale;

        using var reveal = Motion.PushSwitch("##aht_settings_tab", (int)activeTab);
        using var group = ImRaii.Group();
        ImGui.Dummy(new Vector2(0f, 2f * scale));
        PageHeader.Draw(Loc.T(entry.Label), Loc.T(entry.Subtitle));

        switch (activeTab)
        {
            case Tab.General:
                GeneralSettings.Draw(configuration);
                break;
            case Tab.Feed:
                FeedSettings.Draw(configuration);
                NotificationSettings.Draw(configuration);
                RelaySettings.Draw(configuration);
                break;
            case Tab.Ride:
                ConductorSettings.Draw(configuration);
                TravelSettings.Draw(configuration);
                EngagementSettings.Draw(configuration);
                AfterRideSettings.Draw(configuration);
                break;
            case Tab.Humanizer:
                HumanizerSettings.Draw(configuration);
                break;
            case Tab.Upkeep:
                UpkeepSettings.Draw(configuration);
                break;
            case Tab.Party:
                PartySettings.Draw(configuration);
                break;
            case Tab.GmAlert:
                GmAlertSettings.Draw(configuration);
                break;
        }
    }
}
