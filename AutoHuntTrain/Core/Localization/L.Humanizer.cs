namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Humanizer
    {
        public static readonly LocString Tab = new("humanizer.tab", "Humanizer");
        public static readonly LocString TabSub = new("humanizer.tabSub", "Short random pauses before the ride acts, the way a player reacts.");
        public static readonly LocString Group = new("humanizer.group", "Reaction delays");
        public static readonly LocString Enabled = new("humanizer.enabled", "Humanize reactions");
        public static readonly LocString EnabledHelp = new("humanizer.enabledHelp", "Wait a random moment before each action below, drawn anew each time between the shortest and the longest value. A shortest value above the longest is used as a fixed wait. Off acts at once.");
        public static readonly LocString RangeTo = new("humanizer.rangeTo", "to");
        public static readonly LocString SecondsFormat = new("humanizer.secondsFormat", "%.1f s");
        public static readonly LocString MoveOff = new("humanizer.moveOff", "Moving off to a new flag");
        public static readonly LocString MoveOffHelp = new("humanizer.moveOffHelp", "After the conductor posts a new flag, before any teleport or move toward it. A newer flag during the wait starts over with its own wait.");
        public static readonly LocString Teleport = new("humanizer.teleport", "Casting a teleport");
        public static readonly LocString TeleportHelp = new("humanizer.teleportHelp", "Before each teleport the ride casts, on the way to a flag and while catching up with a train.");
        public static readonly LocString Engage = new("humanizer.engage", "Engaging a pulled mark");
        public static readonly LocString EngageHelp = new("humanizer.engageHelp", "Once someone has pulled the mark, before joining the fight. Skipped when the mark is already under half health.");
        public static readonly LocString Invite = new("humanizer.invite", "Accepting a party invite");
        public static readonly LocString InviteHelp = new("humanizer.inviteHelp", "Before accepting a party invite during a ride.");
        public static readonly LocString Shout = new("humanizer.shout", "Looking-for-group shout");
        public static readonly LocString ShoutHelp = new("humanizer.shoutHelp", "After arriving at the train's start, before the looking-for-group shout goes out.");
        public static readonly LocString TeleportOffer = new("humanizer.teleportOffer", "Accepting a party teleport");
        public static readonly LocString TeleportOfferHelp = new("humanizer.teleportOfferHelp", "Before accepting a party member's teleport offer during a ride.");
    }
}
