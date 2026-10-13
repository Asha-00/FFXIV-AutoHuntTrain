using AutoHuntTrain.Core.Game;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class PartySettings
{
    private const float TextInputWidth = 360f;
    // The chat box refuses a line longer than 500 bytes, and "/shout " takes a few of them.
    private const int ShoutMaxLength = 480;

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Safety.PartyGroup));

        SettingsRow.Draw(Loc.T(L.Safety.AcceptInvites),
            Loc.T(L.Safety.AcceptInvitesHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AcceptPartyInvites, value => configuration.AcceptPartyInvites = value, "##aht_party_accept"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Safety.LookingForGroup),
            Loc.T(L.Safety.LookingForGroupHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.PostLookingForGroup, value => configuration.PostLookingForGroup = value, "##aht_party_lfg"),
            SettingsRow.ToggleHeight);

        using (var text = Motion.PushSection("##aht_party_lfg_text", configuration.PostLookingForGroup))
        {
            if (text is not null)
            {
                SettingsRow.DrawBlock(Loc.T(L.Safety.LookingForGroupText),
                    Loc.T(L.Safety.LookingForGroupTextHelp),
                    () => DrawShoutInput(configuration));
            }
        }

        SettingsRow.Draw(Loc.T(L.Safety.PartyFinderListing),
            Loc.T(L.Safety.PartyFinderListingHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.CreatePartyFinderListing, value => configuration.CreatePartyFinderListing = value, "##aht_party_pf"),
            SettingsRow.ToggleHeight);

        using (var comment = Motion.PushSection("##aht_party_pf_comment", configuration.CreatePartyFinderListing))
        {
            if (comment is not null)
            {
                SettingsRow.DrawBlock(Loc.T(L.Safety.PartyFinderComment),
                    Loc.T(L.Safety.PartyFinderCommentHelp),
                    () => DrawCommentInput(configuration));
            }
        }

        SettingsRow.Draw(Loc.T(L.Safety.LeaveParty),
            Loc.T(L.Safety.LeavePartyHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.LeavePartyAfterRide, value => configuration.LeavePartyAfterRide = value, "##aht_party_leave"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawShoutInput(Configuration configuration)
    {
        var text = configuration.LookingForGroupText;
        if (!DrawTextInput("##aht_party_lfg_input", ref text, ShoutMaxLength))
        {
            return;
        }

        configuration.LookingForGroupText = text;
        configuration.SaveDebounced();
    }

    private static void DrawCommentInput(Configuration configuration)
    {
        var text = configuration.PartyFinderComment ?? string.Empty;
        if (!DrawTextInput("##aht_party_pf_comment_input", ref text, PartyFinderListing.CommentMaxBytes))
        {
            return;
        }

        configuration.PartyFinderComment = text;
        configuration.SaveDebounced();
    }

    private static bool DrawTextInput(string id, ref string text, int maxLength)
    {
        ImGui.SetNextItemWidth(TextInputWidth * ImGuiHelpers.GlobalScale);
        using (SettingsControls.PushFrameColors())
        {
            return ImGui.InputText(id, ref text, maxLength);
        }
    }
}
