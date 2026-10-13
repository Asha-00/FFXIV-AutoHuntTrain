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
        public static readonly LocString CombatMovement = new("humanizer.combatMovement", "Combat movement");
        public static readonly LocString CombatMovementEnable = new("humanizer.combatMovementEnable", "Vary dodging in combat");
        public static readonly LocString CombatMovementEnableHelp = new("humanizer.combatMovementEnableHelp", "BossMod steps out of an AoE the moment it is telegraphed and stops right at its edge, so everyone on a mark who runs this plugin dodges on the same frame to the same spot. When on, each mark rolls a dodge delay, a movement delay and a preference for extra room from the ranges below. The combat preset itself is never changed.");
        public static readonly LocString CombatMovementOff = new("humanizer.combatMovementOff", "Combat movement variation is off. BossMod dodges the way the combat preset is set up.");
        public static readonly LocString CombatMovementNoModule = new("humanizer.combatMovementNoModule", "The combat preset '{0}' has no Automatic movement (AI) module, so these settings do nothing there. Add that module to the preset in BossMod, or delete the preset there so the next ride puts the bundled one back.");
        public static readonly LocString DodgeDelay = new("humanizer.dodgeDelay", "Dodge delay");
        public static readonly LocString DodgeDelayHelp = new("humanizer.dodgeDelayHelp", "How long BossMod waits before stepping out of a telegraphed AoE. One value is rolled per mark from this range. Longer delays mean getting hit more often and more knockouts, and casters may see more casts cancelled.");
        public static readonly LocString MoveDelay = new("humanizer.moveDelay", "Movement delay");
        public static readonly LocString MoveDelayHelp = new("humanizer.moveDelayHelp", "How long BossMod waits before any other repositioning, such as getting back into range of the mark. It only applies when starting to move from standing still. Most players can leave this at None.");
        public static readonly LocString DangerRoom = new("humanizer.dangerRoom", "Prefer extra room from danger");
        public static readonly LocString DangerRoomHelp = new("humanizer.dangerRoomHelp", "Asks BossMod to prefer spots further inside safe ground rather than the very edge of an AoE. This is a soft preference, not a guaranteed distance: it depends on BossMod's map resolution, and the lower levels may change nothing at all.");
        public static readonly LocString LevelNone = new("humanizer.levelNone", "None");
        public static readonly LocString DelayShort = new("humanizer.delayShort", "0.5 s");
        public static readonly LocString DelayLong = new("humanizer.delayLong", "1 s");
        public static readonly LocString RoomSmall = new("humanizer.roomSmall", "Small");
        public static readonly LocString RoomMedium = new("humanizer.roomMedium", "Medium");
        public static readonly LocString RoomLarge = new("humanizer.roomLarge", "Large");
    }
}
