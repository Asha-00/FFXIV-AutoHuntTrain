namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Details
    {
        public static readonly LocString Back = new("details.back", "All hunts");
        public static readonly LocString Title = new("details.title", "{0} train on {1}");
        public static readonly LocString TitleSRank = new("details.title.sRank", "{0} on {1}");
        public static readonly LocString WorldLine = new("details.worldLine", "{0}  ·  {1}  ·  {2}");
        public static readonly LocString BadgeTrain = new("details.badge.train", "Train");
        public static readonly LocString BadgeSRank = new("details.badge.sRank", "S Rank");
        public static readonly LocString GroupCenturio = new("details.group.centurio", "Centurio");
        public static readonly LocString GroupShadowbringers = new("details.group.shb", "Shadowbringers");
        public static readonly LocString GroupEndwalker = new("details.group.ew", "Endwalker");
        public static readonly LocString GroupDawntrail = new("details.group.dt", "Dawntrail");

        public static readonly LocString LabelStart = new("details.label.start", "Starts");
        public static readonly LocString LabelReported = new("details.label.reported", "Reported");
        public static readonly LocString LabelStartZone = new("details.label.startZone", "Start zone");
        public static readonly LocString LabelAetheryte = new("details.label.aetheryte", "Aetheryte");
        public static readonly LocString LabelConductor = new("details.label.conductor", "Conductor");
        public static readonly LocString LabelCreature = new("details.label.creature", "Creature");
        public static readonly LocString LabelPosted = new("details.label.posted", "Posted");
        public static readonly LocString LabelInstance = new("details.label.instance", "Instance");
        public static readonly LocString LabelFlag = new("details.label.flag", "Map spot");
        public static readonly LocString LabelAnnouncement = new("details.label.announcement", "Announcement");
        public static readonly LocString NotNamed = new("details.notNamed", "Not named");
        public static readonly LocString NoBody = new("details.noBody", "The announcement carried no text beyond its details.");
        public static readonly LocString CatchUpNote = new("details.catchUpNote", "Riding now catches up with the train along its usual route");

        public static readonly LocString Flag = new("details.flag", "Flag on map");
        public static readonly LocString FlagHint = new("details.flagHint", "Open the map with a flag on the train's start");
        public static readonly LocString FlagHintSRank = new("details.flagHint.sRank", "Open the map with a flag on the S rank's reported location");
        public static readonly LocString PartyFinder = new("details.partyFinder", "Party Finder");
        public static readonly LocString PartyFinderHint = new("details.partyFinderHint", "Open the Party Finder on the Hunts category");
        public static readonly LocString Nav = new("details.nav", "Nav");
        public static readonly LocString NavHint = new("details.navHint", "Point an on-screen arrow at the train's start while you are in {0} on {1}. Right-click the arrow to clear it.");
        public static readonly LocString NavHintSRank = new("details.navHint.sRank", "Point an on-screen arrow at the S rank's reported location while you are in {0} on {1}. Right-click the arrow to clear it.");
        public static readonly LocString NavOn = new("details.navOn", "Nav on");
        public static readonly LocString NavOnHint = new("details.navOnHint", "The arrow points at this train's start. Click to clear it.");
        public static readonly LocString NavSet = new("details.navSet", "The navigation arrow shows once you are in {0} on {1}.");
        public static readonly LocString NavSetHere = new("details.navSetHere", "The navigation arrow points at the train's start.");
        public static readonly LocString NavDistance = new("details.navDistance", "{0} y");
        public static readonly LocString NavArrived = new("details.navArrived", "Arrived");
        public static readonly LocString NavTooltip = new("details.navTooltip", "Train start {0}\nRight-click to clear the arrow");
        public static readonly LocString Relay = new("details.relay", "Relay");
        public static readonly LocString RelayHint = new("details.relayHint", "Post a one-line summary of this train to {0}");
        public static readonly LocString RelayHintSRank = new("details.relayHint.sRank", "Post a one-line summary of this S rank to {0}");
        public static readonly LocString RelayPick = new("details.relayPick", "Relay to another channel");
        public static readonly LocString RelayText = new("details.relay.text", "{0} train on {1}!");
        public static readonly LocString RelayTextSRank = new("details.relay.text.sRank", "{0} on {1}!");
        public static readonly LocString InstanceShort = new("details.relay.instance", "i{0}");

        public static readonly LocString ChannelSay = new("details.channel.say", "Say");
        public static readonly LocString ChannelYell = new("details.channel.yell", "Yell");
        public static readonly LocString ChannelShout = new("details.channel.shout", "Shout");
        public static readonly LocString ChannelParty = new("details.channel.party", "Party");
        public static readonly LocString ChannelAlliance = new("details.channel.alliance", "Alliance");
        public static readonly LocString ChannelFreeCompany = new("details.channel.freeCompany", "Free Company");
        public static readonly LocString ChannelLinkshell = new("details.channel.linkshell", "Linkshell {0}");
        public static readonly LocString ChannelCrossWorldLinkshell = new("details.channel.crossWorldLinkshell", "Cross-world linkshell {0}");
        public static readonly LocString ChannelEcho = new("details.channel.echo", "Echo (only you)");

        public static readonly LocString SettingsGroup = new("details.settings.group", "Train details");
        public static readonly LocString RelayChannel = new("details.settings.relayChannel", "Relay channel");
        public static readonly LocString RelayChannelHelp = new("details.settings.relayChannelHelp", "Where the Relay button on a train's details posts its one-line summary. The arrow next to the button picks another channel for a single post.");
        public static readonly LocString RelayFlag = new("details.settings.relayFlag", "Relay with the map flag");
        public static readonly LocString RelayFlagHelp = new("details.settings.relayFlagHelp", "When the train's start has map coordinates, place your map flag there and post <flag> in their place, so the channel gets a spot to click.");
    }
}
