namespace AutoHuntTrain.Core.Ipc;

// The combat plugin steps out of a telegraphed attack the instant it shows and stops right at its edge, so every
// character on a mark that runs this plugin dodges on the same frame to the same spot. Rolling NormalMovement's delays and
// cushion per mark breaks that lockstep, where a fixed value would only shift them all together. The values are
// transients on the combat plugin's in-memory preset, and those outlast this plugin, so they are taken back at the end of
// every mark, a stopped ride and an unload.
internal static class DodgeTuning
{
    private const string SeparateDodgeDelayTrack = "SeparateDodgeDelay";
    private const string DodgeDelayTrack = "DodgeDelayMovement";
    private const string MovementDelayTrack = "DelayMovement";
    private const string CushionTrack = "ForbiddenZoneCushion";
    private const string EnabledOption = "Enabled";

    // The combat plugin's option names in its own order; the settings hold indexes into these.
    public static readonly string[] DelayOptions = ["None", "Short", "Long"];
    public static readonly string[] CushionOptions = ["None", "Small", "Medium", "Large"];

    private static readonly string[] tracks = [SeparateDodgeDelayTrack, DodgeDelayTrack, MovementDelayTrack, CushionTrack];
    private static readonly List<string> tunedPresets = [];

    public static DodgeRoll Roll(Configuration configuration)
        => new(
            RollLevel(DelayOptions, configuration.DodgeDelayMinLevel, configuration.DodgeDelayMaxLevel),
            RollLevel(DelayOptions, configuration.MovementDelayMinLevel, configuration.MovementDelayMaxLevel),
            RollLevel(CushionOptions, configuration.DangerRoomMinLevel, configuration.DangerRoomMaxLevel));

    // A dodge delay split off without its rolled value is worse than none, so a refused track takes back the whole roll.
    public static bool Apply(string preset, in DodgeRoll roll)
    {
        var bossMod = BossModIPC.Instance;
        var module = HuntCombatPreset.MovementModule;
        var applied = bossMod.AddTransientStrategy(preset, module, SeparateDodgeDelayTrack, EnabledOption)
            && bossMod.AddTransientStrategy(preset, module, DodgeDelayTrack, roll.DodgeDelayOption)
            && bossMod.AddTransientStrategy(preset, module, MovementDelayTrack, roll.MovementDelayOption)
            && bossMod.AddTransientStrategy(preset, module, CushionTrack, roll.CushionOption);
        if (!applied)
        {
            ClearTracks(preset);
            return false;
        }

        if (!tunedPresets.Contains(preset))
        {
            tunedPresets.Add(preset);
        }

        return true;
    }

    public static void Release()
    {
        for (var presetIndex = 0; presetIndex < tunedPresets.Count; presetIndex++)
        {
            var preset = tunedPresets[presetIndex];
            ClearTracks(preset);
            RunLog.Info($"Hunt: the dodge tuning on the '{preset}' preset was taken back");
        }

        tunedPresets.Clear();
    }

    // Null when the combat plugin cannot hand the preset back, so nothing is claimed about a preset it never showed.
    public static bool? PresetHasMovementModule(string preset)
        => BossModIPC.Instance.GetPreset(preset) is { } serialized
            ? serialized.Contains(HuntCombatPreset.MovementModule, StringComparison.Ordinal)
            : null;

    private static void ClearTracks(string preset)
    {
        var bossMod = BossModIPC.Instance;
        for (var trackIndex = 0; trackIndex < tracks.Length; trackIndex++)
        {
            bossMod.ClearTransientStrategy(preset, HuntCombatPreset.MovementModule, tracks[trackIndex]);
        }
    }

    private static byte RollLevel(string[] options, int minimum, int maximum)
    {
        var last = options.Length - 1;
        return (byte)Humanizer.RollBetween(Math.Clamp(minimum, 0, last), Math.Clamp(maximum, 0, last));
    }
}

internal readonly record struct DodgeRoll(byte DodgeDelay, byte MovementDelay, byte Cushion)
{
    public string DodgeDelayOption => DodgeTuning.DelayOptions[DodgeDelay];

    public string MovementDelayOption => DodgeTuning.DelayOptions[MovementDelay];

    public string CushionOption => DodgeTuning.CushionOptions[Cushion];
}
