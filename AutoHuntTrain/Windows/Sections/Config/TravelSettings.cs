using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class TravelSettings
{
    private const int TeleportWaitSecondsMin = 0;
    private const int TeleportWaitSecondsStep = 15;

    private static readonly FlagTeleportMode[] teleportModeOrder = [FlagTeleportMode.TeleportMyself, FlagTeleportMode.WaitForTeleport];

    // Ordered like teleportModeOrder, because the picked index reads both.
    private static readonly SettingsControls.Choices.Choice[] teleportModeChoices =
    [
        new(L.Ride.TeleportMyselfName, L.Ride.TeleportMyselfDetail),
        new(L.Ride.WaitForTeleportName, L.Ride.WaitForTeleportDetail),
    ];

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Ride.TravelGroup));

        var selected = Math.Max(0, Array.IndexOf(teleportModeOrder, configuration.FlagTeleportMode));
        SettingsRow.Draw(Loc.T(L.Ride.FlagTeleport),
            Loc.T(L.Ride.FlagTeleportHelp),
            SettingsControls.RowComboWidth,
            () => SettingsControls.Choices.DrawCombo("##aht_travel_flagteleport", teleportModeChoices, selected, choice =>
            {
                configuration.FlagTeleportMode = teleportModeOrder[choice];
                configuration.SaveDebounced();
            }));
        SettingsRow.Caption(Loc.T(teleportModeChoices[selected].Detail));

        using (var wait = Motion.PushSection("##aht_travel_teleportwait", configuration.FlagTeleportMode == FlagTeleportMode.WaitForTeleport))
        {
            if (wait is not null)
            {
                SettingsRow.Draw(Loc.T(L.Ride.TeleportWait),
                    Loc.T(L.Ride.TeleportWaitHelp),
                    Stepper.DefaultWidth,
                    () => DrawTeleportWaitStepper(configuration));
            }
        }

        SettingsRow.Draw(Loc.T(L.Ride.AcceptTeleports),
            Loc.T(L.Ride.AcceptTeleportsHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AcceptPartyTeleports, value => configuration.AcceptPartyTeleports = value, "##aht_travel_acceptteleports"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawTeleportWaitStepper(Configuration configuration)
    {
        var seconds = configuration.FlagTeleportWaitSeconds;
        if (!Stepper.Draw("##aht_travel_teleportwait_seconds", ref seconds, TeleportWaitSecondsStep, TeleportWaitSecondsMin, Configuration.FlagTeleportWaitSecondsMax, Loc.T(L.Safety.SecondsFormat)))
        {
            return;
        }

        configuration.FlagTeleportWaitSeconds = seconds;
        configuration.SaveDebounced();
    }
}
