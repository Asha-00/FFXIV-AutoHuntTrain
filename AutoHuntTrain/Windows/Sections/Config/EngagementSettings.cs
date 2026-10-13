using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class EngagementSettings
{
    private const int PullWaitSecondsMin = 15;
    private const int PullWaitSecondsMax = 600;
    private const int PullWaitSecondsStep = 15;

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Ride.EngagementGroup));

        SettingsRow.Draw(Loc.T(L.Ride.WaitForPull),
            Loc.T(L.Ride.WaitForPullHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.WaitForPull, value => configuration.WaitForPull = value, "##aht_engage_waitpull"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Ride.PullWait),
            Loc.T(L.Ride.PullWaitHelp),
            Stepper.DefaultWidth,
            () => DrawPullWaitStepper(configuration));

        SettingsRow.Draw(Loc.T(L.Ride.EndWhenAllCredited),
            Loc.T(L.Ride.EndWhenAllCreditedHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.EndWhenAllCredited, value => configuration.EndWhenAllCredited = value, "##aht_engage_endcredited"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Ride.TagWithRanged),
            Loc.T(L.Ride.TagWithRangedHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.TagWithRangedAttacks, value => configuration.TagWithRangedAttacks = value, "##aht_engage_tagranged"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawPullWaitStepper(Configuration configuration)
    {
        var seconds = configuration.PullWaitSeconds;
        if (!Stepper.Draw("##aht_engage_pullwait", ref seconds, PullWaitSecondsStep, PullWaitSecondsMin, PullWaitSecondsMax, Loc.T(L.Safety.SecondsFormat)))
        {
            return;
        }

        configuration.PullWaitSeconds = seconds;
        configuration.SaveDebounced();
    }
}
