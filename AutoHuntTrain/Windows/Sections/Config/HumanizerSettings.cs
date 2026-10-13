using AutoHuntTrain.Core;
using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class HumanizerSettings
{
    private const float RangeStepperWidth = 118f;
    private const float RangeGap = 8f;
    // Fetching a preset serializes it on the combat plugin's side, so the module check is refreshed only this often.
    private const long ModuleCheckIntervalMs = 5_000;

    private readonly record struct DelayRow(HumanAction Action, LocString Label, LocString Help, string MinId, string MaxId);

    private static readonly DelayRow[] rows =
    [
        new(HumanAction.MoveOff, L.Humanizer.MoveOff, L.Humanizer.MoveOffHelp, "##aht_human_moveoff_min", "##aht_human_moveoff_max"),
        new(HumanAction.Teleport, L.Humanizer.Teleport, L.Humanizer.TeleportHelp, "##aht_human_teleport_min", "##aht_human_teleport_max"),
        new(HumanAction.Engage, L.Humanizer.Engage, L.Humanizer.EngageHelp, "##aht_human_engage_min", "##aht_human_engage_max"),
        new(HumanAction.AcceptInvite, L.Humanizer.Invite, L.Humanizer.InviteHelp, "##aht_human_invite_min", "##aht_human_invite_max"),
        new(HumanAction.AcceptTeleport, L.Humanizer.TeleportOffer, L.Humanizer.TeleportOfferHelp, "##aht_human_teleportoffer_min", "##aht_human_teleportoffer_max"),
        new(HumanAction.LookingForGroup, L.Humanizer.Shout, L.Humanizer.ShoutHelp, "##aht_human_shout_min", "##aht_human_shout_max"),
        new(HumanAction.PartyFinder, L.Humanizer.PartyFinder, L.Humanizer.PartyFinderHelp, "##aht_human_partyfinder_min", "##aht_human_partyfinder_max"),
    ];

    // Ordered like the combat plugin's options in DodgeTuning.
    private static readonly LocString[] delayLevelNames = [L.Humanizer.LevelNone, L.Humanizer.DelayShort, L.Humanizer.DelayLong];
    private static readonly LocString[] roomLevelNames = [L.Humanizer.LevelNone, L.Humanizer.RoomSmall, L.Humanizer.RoomMedium, L.Humanizer.RoomLarge];
    private static readonly string[] delayLabels = new string[delayLevelNames.Length];
    private static readonly string[] roomLabels = new string[roomLevelNames.Length];

    private static long moduleCheckAtMs;
    private static int presetMissingModule = -1;
    private static CachedText missingModuleNote;

    public static void Draw(Configuration configuration)
    {
        DrawReactionGroup(configuration);
        DrawCombatMovementGroup(configuration);
    }

    private static void DrawReactionGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Humanizer.Group));

        SettingsRow.Draw(Loc.T(L.Humanizer.Enabled),
            Loc.T(L.Humanizer.EnabledHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.HumanizerEnabled, value => configuration.HumanizerEnabled = value, "##aht_human_enabled"),
            SettingsRow.ToggleHeight);

        using var delays = Motion.PushSection("##aht_human_delays", configuration.HumanizerEnabled);
        if (delays is null)
        {
            return;
        }

        var controlWidth = RangeWidth();
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            SettingsRow.Draw(Loc.T(row.Label), Loc.T(row.Help), controlWidth, () => DrawRange(configuration, row));
        }
    }

    private static void DrawCombatMovementGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Humanizer.CombatMovement));

        SettingsRow.Draw(Loc.T(L.Humanizer.CombatMovementEnable),
            Loc.T(L.Humanizer.CombatMovementEnableHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.CombatMovementEnabled, value => configuration.CombatMovementEnabled = value, "##aht_human_combat_on"),
            SettingsRow.ToggleHeight);

        using var body = Motion.PushSwitch("##aht_human_combat_body", configuration.CombatMovementEnabled);
        if (!configuration.CombatMovementEnabled)
        {
            SettingsRow.Note(Loc.T(L.Humanizer.CombatMovementOff));
            return;
        }

        ResolveLabels(delayLevelNames, delayLabels);
        ResolveLabels(roomLevelNames, roomLabels);
        var controlWidth = SettingsControls.ComboRangeWidth();

        SettingsRow.Draw(Loc.T(L.Humanizer.DodgeDelay),
            Loc.T(L.Humanizer.DodgeDelayHelp),
            controlWidth,
            () => SettingsControls.DrawComboRange(configuration, "##aht_human_dodge_min", "##aht_human_dodge_max", delayLabels,
                () => configuration.DodgeDelayMinLevel, value => configuration.DodgeDelayMinLevel = value,
                () => configuration.DodgeDelayMaxLevel, value => configuration.DodgeDelayMaxLevel = value));

        SettingsRow.Draw(Loc.T(L.Humanizer.MoveDelay),
            Loc.T(L.Humanizer.MoveDelayHelp),
            controlWidth,
            () => SettingsControls.DrawComboRange(configuration, "##aht_human_move_min", "##aht_human_move_max", delayLabels,
                () => configuration.MovementDelayMinLevel, value => configuration.MovementDelayMinLevel = value,
                () => configuration.MovementDelayMaxLevel, value => configuration.MovementDelayMaxLevel = value));

        SettingsRow.Draw(Loc.T(L.Humanizer.DangerRoom),
            Loc.T(L.Humanizer.DangerRoomHelp),
            controlWidth,
            () => SettingsControls.DrawComboRange(configuration, "##aht_human_room_min", "##aht_human_room_max", roomLabels,
                () => configuration.DangerRoomMinLevel, value => configuration.DangerRoomMinLevel = value,
                () => configuration.DangerRoomMaxLevel, value => configuration.DangerRoomMaxLevel = value));

        var missing = PresetMissingMovementModule();
        if (missing >= 0)
        {
            SettingsRow.Note(missingModuleNote.Get(missing, static index => Loc.T(L.Humanizer.CombatMovementNoModule, HuntCombatPreset.Names[index])), Styling.AccentAmber);
        }
    }

    // The catalog hands back cached strings, so refilling the label buffers every frame allocates nothing.
    private static void ResolveLabels(LocString[] names, string[] labels)
    {
        for (var index = 0; index < names.Length; index++)
        {
            labels[index] = Loc.T(names[index]);
        }
    }

    // The first bundled preset without the module, or -1 when each has it or the combat plugin did not answer.
    private static int PresetMissingMovementModule()
    {
        var now = Environment.TickCount64;
        if (now < moduleCheckAtMs)
        {
            return presetMissingModule;
        }

        moduleCheckAtMs = now + ModuleCheckIntervalMs;
        presetMissingModule = -1;
        var presets = HuntCombatPreset.Names;
        for (var presetIndex = 0; presetIndex < presets.Length; presetIndex++)
        {
            if (DodgeTuning.PresetHasMovementModule(presets[presetIndex]) == false)
            {
                presetMissingModule = presetIndex;
                break;
            }
        }

        return presetMissingModule;
    }

    // In unscaled units, as SettingsRow scales the control width itself.
    private static float RangeWidth()
        => RangeStepperWidth * 2f + RangeGap * 2f + TextDraw.Measure(Loc.T(L.Humanizer.RangeTo)).X / ImGuiHelpers.GlobalScale;

    private static void DrawRange(Configuration configuration, in DelayRow row)
    {
        var gap = RangeGap * ImGuiHelpers.GlobalScale;
        var format = Loc.T(L.Humanizer.SecondsFormat);
        var range = configuration.DelayFor(row.Action);
        var shortest = range.MinSeconds;
        var longest = range.MaxSeconds;

        var changed = Stepper.Draw(row.MinId, ref shortest, Humanizer.SecondsStep, Humanizer.SecondsMin, Humanizer.SecondsMax, format, RangeStepperWidth);
        ImGui.SameLine(0f, gap);
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextMuted))
        {
            ImGui.TextUnformatted(Loc.T(L.Humanizer.RangeTo));
        }

        ImGui.SameLine(0f, gap);
        changed |= Stepper.Draw(row.MaxId, ref longest, Humanizer.SecondsStep, Humanizer.SecondsMin, Humanizer.SecondsMax, format, RangeStepperWidth);
        if (!changed)
        {
            return;
        }

        configuration.SetDelay(row.Action, new DelayRange(shortest, longest));
        configuration.SaveDebounced();
    }
}
