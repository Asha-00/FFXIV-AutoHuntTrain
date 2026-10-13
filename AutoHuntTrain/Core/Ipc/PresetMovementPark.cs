namespace AutoHuntTrain.Core.Ipc;

// Parking a combat preset's movement hands the walk to the pathfinder while the rotation keeps attacking. The park is a
// transient on the combat plugin's in-memory preset, and those outlast this plugin, so one still held when a ride stops
// or the plugin unloads is lifted there too.
internal static class PresetMovementPark
{
    private const string DestinationTrack = "Destination";
    private const string ParkedOption = "None";

    private static readonly List<string> parkedPresets = [];

    // Only parked when the combat plugin can lift the park again.
    public static bool Park(string preset)
    {
        var bossMod = BossModIPC.Instance;
        if (!bossMod.CanClearTransientStrategy
            || !bossMod.AddTransientStrategy(preset, HuntCombatPreset.MovementModule, DestinationTrack, ParkedOption))
        {
            return false;
        }

        if (!parkedPresets.Contains(preset))
        {
            parkedPresets.Add(preset);
        }

        return true;
    }

    // A park a stop or an unload lifted already counts as lifted.
    public static bool Resume(string preset)
        => !parkedPresets.Remove(preset) || Lift(preset);

    public static void Release()
    {
        var parkedCount = parkedPresets.Count;
        if (parkedCount == 0)
        {
            return;
        }

        for (var presetIndex = 0; presetIndex < parkedCount; presetIndex++)
        {
            Lift(parkedPresets[presetIndex]);
        }

        parkedPresets.Clear();
        RunLog.Info($"Hunt: the movement park was lifted from {parkedCount} combat preset(s)");
    }

    private static bool Lift(string preset)
        => BossModIPC.Instance.ClearTransientStrategy(preset, HuntCombatPreset.MovementModule, DestinationTrack);
}
