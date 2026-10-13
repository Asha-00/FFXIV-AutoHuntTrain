namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Feed
    {
        public static readonly LocString HeadlineHuntAlerts = new("feed.headline.huntAlerts", "Feed: HuntAlerts");
        public static readonly LocString HeadlineDetail = new("feed.headline.detail", "{0}  ·  {1}");
        public static readonly LocString AutoJoinOff = new("feed.autoJoin.off", "Auto-join off");
        public static readonly LocString AutoJoinOn = new("feed.autoJoin.on", "Auto-join: {0}");
        public static readonly LocString AutoJoinEvery = new("feed.autoJoin.every", "Auto-join: every expansion");
        public static readonly LocString AutoJoinSeparator = new("feed.autoJoin.separator", ", ");
        public static readonly LocString SnoozedUntil = new("feed.autoJoin.snoozedUntil", "{0}  ·  snoozed until {1}");

        public static readonly LocString BadgeCenturio = new("feed.badge.centurio", "Centurio");
        public static readonly LocString BadgeShadowbringers = new("feed.badge.shb", "ShB");
        public static readonly LocString BadgeEndwalker = new("feed.badge.ew", "EW");
        public static readonly LocString BadgeDawntrail = new("feed.badge.dt", "DT");
        public static readonly LocString WorldLine = new("feed.worldLine", "{0}  ·  {1}");
        public static readonly LocPlural InMinutes = new("feed.time.inMinutes", "in {0} minute", "in {0} minutes");
        public static readonly LocPlural StartedAgo = new("feed.time.startedAgo", "started {0} minute ago", "started {0} minutes ago");
        public static readonly LocString StartingNow = new("feed.time.startingNow", "starting now");
        public static readonly LocPlural ReportedAgo = new("feed.time.reportedAgo", "reported {0} minute ago", "reported {0} minutes ago");
        public static readonly LocString ReachSameWorld = new("feed.reach.sameWorld", "This world");
        public static readonly LocString ReachSameDataCenter = new("feed.reach.sameDataCenter", "Same data center");
        public static readonly LocString ReachCrossDataCenter = new("feed.reach.crossDataCenter", "Other data center");
        public static readonly LocString ReachOutOfRegion = new("feed.reach.outOfRegion", "Other region");
        public static readonly LocString RegionNorthAmerica = new("feed.region.northAmerica", "NA");
        public static readonly LocString RegionEurope = new("feed.region.europe", "EU");
        public static readonly LocString RegionJapan = new("feed.region.japan", "JP");
        public static readonly LocString RegionOceania = new("feed.region.oceania", "OCE");
        public static readonly LocString RegionUnknown = new("feed.region.unknown", "??");
        public static readonly LocString Ride = new("feed.ride", "Ride");
        public static readonly LocString RideHint = new("feed.rideHint", "Travel to the start of this train and follow it");
        public static readonly LocString RideCatchUpHint = new("feed.rideCatchUpHint", "The train has moved on from its start; the ride catches up with it");
        public static readonly LocString SHunt = new("feed.sHunt", "S-Hunt");
        public static readonly LocString SHuntHint = new("feed.sHuntHint", "Travel to this S rank, wait for another player to pull, then join the fight");
        public static readonly LocString AutoMarker = new("feed.autoMarker", "Auto");
        public static readonly LocString InProgress = new("feed.inProgress", "In progress");

        public static readonly LocString VerdictOutOfRegion = new("feed.verdict.outOfRegion", "This train runs in another region");
        public static readonly LocString VerdictInDuty = new("feed.verdict.inDuty", "Leave the duty or its queue first");
        public static readonly LocString VerdictLifestreamBusy = new("feed.verdict.lifestreamBusy", "Lifestream is busy with something else");
        public static readonly LocString VerdictRideRunning = new("feed.verdict.rideRunning", "A ride is already running");
        public static readonly LocString VerdictSRankCrossDataCenter = new("feed.verdict.sRankCrossDataCenter", "S-Hunts can visit another world on this data center, but not another data center");
        public static readonly LocString VerdictFeedWorldUnknown = new("feed.verdict.feedWorldUnknown", "The announced world is not one this client knows");

        public static readonly LocString PhaseJourney = new("feed.phase.journey", "Travelling to the train");
        public static readonly LocString ConductorNamed = new("feed.conductor.named", "Conductor: {0}");
        public static readonly LocString ConductorAnnounced = new("feed.conductor.announced", "Conductor: {0}, named in the announcement");
        public static readonly LocString ConductorPicked = new("feed.conductor.picked", "Conductor: {0}, picked from the first flag");
        public static readonly LocString ConductorPendingIn = new("feed.conductor.pendingIn", "The first player to post a flag in {0} becomes the conductor");
        public static readonly LocString ConductorPending = new("feed.conductor.pending", "The first player to post a flag becomes the conductor");

        public static readonly LocString SettingsGroup = new("feed.settings.group", "Feed");
        public static readonly LocString AutoJoinGroup = new("feed.settings.autoJoinGroup", "Auto-join");
        public static readonly LocString AutoJoinCenturio = new("feed.settings.autoJoinCenturio", "Auto-join Centurio trains");
        public static readonly LocString AutoJoinShadowbringers = new("feed.settings.autoJoinShb", "Auto-join Shadowbringers trains");
        public static readonly LocString AutoJoinEndwalker = new("feed.settings.autoJoinEw", "Auto-join Endwalker trains");
        public static readonly LocString AutoJoinDawntrail = new("feed.settings.autoJoinDt", "Auto-join Dawntrail trains");
        public static readonly LocString AutoJoinHelp = new("feed.settings.autoJoinHelp", "Trains of this expansion are joined without a click when they pass the rules below. Every train can always be ridden with its Ride button.");
        public static readonly LocString DataCenters = new("feed.settings.dataCenters", "Allowed data centers");
        public static readonly LocString DataCentersHelp = new("feed.settings.dataCentersHelp", "Auto-join and the train notifications take only trains on the ticked data centers, and the My data centers view lists only them. With none ticked, only your home data center counts.");
        public static readonly LocString AllHidden = new("feed.allHidden", "Trains were announced, but none on your allowed data centers. Pick My region or Everywhere to see the others.");
        public static readonly LocString AllHiddenRegion = new("feed.allHiddenRegion", "Trains were announced, but none in your region. Pick Everywhere to see them all.");
        public static readonly LocString ViewMyDataCenters = new("feed.view.myDataCenters", "My data centers");
        public static readonly LocString ViewMyRegion = new("feed.view.myRegion", "My region");
        public static readonly LocString ViewEverywhere = new("feed.view.everywhere", "Everywhere");
        public static readonly LocString HomeMarker = new("feed.settings.home", "(home)");
        public static readonly LocString NoHomeRegion = new("feed.settings.noHomeRegion", "Log in to list the data centers of your region.");
        public static readonly LocString CrossDataCenter = new("feed.settings.crossDataCenter", "Auto-join on other data centers");
        public static readonly LocString CrossDataCenterHelp = new("feed.settings.crossDataCenterHelp", "Let auto-join take a train that needs a data center transfer; a Ride button click can always cross. The transfer logs the character out and back in, and Lifestream must have data center travel enabled.");
        public static readonly LocString MinimumLead = new("feed.settings.minimumLead", "Lead time for another data center");
        public static readonly LocString MinimumLeadHelp = new("feed.settings.minimumLeadHelp", "Auto-join skips a train on another data center whose start is closer than this, because a data center transfer takes minutes plus a queue. Trains on your own data center are joined as soon as they are announced, and a train that already started is still joined for three minutes after its start.");
        public static readonly LocString SnoozeLength = new("feed.settings.snoozeLength", "Snooze length");
        public static readonly LocString SnoozeLengthHelp = new("feed.settings.snoozeLengthHelp", "How long /aht snooze suspends auto-join and the train notifications. /aht snooze off lifts it early.");
    }
}
