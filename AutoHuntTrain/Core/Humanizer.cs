namespace AutoHuntTrain.Core;

// A player reacts to a flag, a pull or an invite after a moment, and never after the same moment twice, so each wait
// is drawn anew between the action's shortest and longest.
internal static class Humanizer
{
    public const float SecondsMin = 0f;
    public const float SecondsMax = 15f;
    public const float SecondsStep = 0.1f;

    public static int DrawDelayMs(HumanAction action)
    {
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.HumanizerEnabled)
        {
            return 0;
        }

        var range = configuration.DelayFor(action);
        var shortest = Math.Clamp(range.MinSeconds, SecondsMin, SecondsMax);
        // A shortest above the longest reads as a fixed wait.
        var longest = Math.Max(shortest, Math.Clamp(range.MaxSeconds, SecondsMin, SecondsMax));
        var seconds = shortest + (longest - shortest) * Random.Shared.NextSingle();
        return (int)(seconds * TimeUnits.MillisecondsPerSecond);
    }

    public static string Describe(HumanAction action) => action switch
    {
        HumanAction.MoveOff => "moving off",
        HumanAction.Teleport => "casting the teleport",
        HumanAction.Engage => "engaging",
        HumanAction.AcceptInvite => "accepting the invite",
        HumanAction.AcceptTeleport => "accepting the teleport",
        HumanAction.PartyFinder => "putting up the Party Finder listing",
        _ => "the looking-for-group shout",
    };
}
