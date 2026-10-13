namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Ride
    {
        public static readonly LocString ReasonPickConductor = new("ride.reason.pickConductor", "pick a conductor");

        public static readonly LocString CardConductor = new("ride.card.conductor", "Conductor");
        public static readonly LocString CardPickRecent = new("ride.card.pickRecent", "Pick a recent poster");
        public static readonly LocString CardTypeHint = new("ride.card.typeHint", "First Last@World");
        public static readonly LocString CardClear = new("ride.card.clear", "Clear the conductor");
        public static readonly LocString CardFollowing = new("ride.card.following", "Following {0}");
        public static readonly LocString CardNone = new("ride.card.none", "No conductor picked yet. Recent flag posters in Shout, Yell and Say on this world show up in the list.");
        public static readonly LocString CardLatestFlag = new("ride.card.latestFlag", "Latest flag: {0}  ·  {1}");
        public static readonly LocString CardNoFlagYet = new("ride.card.noFlagYet", "No flag from them yet.");
        public static readonly LocString SecondsAgo = new("ride.time.secondsAgo", "{0}s ago");

        public static readonly LocString FlagLine = new("ride.flag.line", "{0}  ·  ({1:F1}, {2:F1})");
        public static readonly LocString FlagLineInstance = new("ride.flag.lineInstance", "{0}  ·  ({1:F1}, {2:F1})  ·  i{3}");
        public static readonly LocString PhaseWaiting = new("ride.phase.waiting", "Waiting for a flag");
        public static readonly LocString PhaseTravelling = new("ride.phase.travelling", "Riding to the flag");
        public static readonly LocString PhaseAtFlag = new("ride.phase.atFlag", "At the flag");
        public static readonly LocString PhaseWaitingForMark = new("ride.phase.waitingForMark", "Waiting for the mark");
        public static readonly LocString PhaseCatchingUp = new("ride.phase.catchingUp", "Catching up");
        public static readonly LocString CatchUpTeleporting = new("ride.catchUp.teleporting", "Catching up: teleporting to {0} ({1} of {2})");
        public static readonly LocString CatchUpListening = new("ride.catchUp.listening", "Catching up: listening in {0} ({1} of {2})");
        public static readonly LocPlural FlagsFollowed = new("ride.flagsFollowed", "{0} flag followed", "{0} flags followed");
        public static readonly LocString ProgressLine = new("ride.progressLine", "{0}  ·  {1}");
        public static readonly LocPlural Credited = new("ride.credited", "{0} credited", "{0} credited");

        public static readonly LocString SettingsGroup = new("ride.settings.group", "Conductor");
        public static readonly LocString ListenShout = new("ride.settings.shout", "Listen to Shout");
        public static readonly LocString ListenShoutHelp = new("ride.settings.shoutHelp", "Follow flags the conductor posts in Shout, the channel most trains run on.");
        public static readonly LocString ListenYell = new("ride.settings.yell", "Listen to Yell");
        public static readonly LocString ListenYellHelp = new("ride.settings.yellHelp", "Follow flags the conductor posts in Yell, which some trains use to spare the Shout channel.");
        public static readonly LocString ListenSay = new("ride.settings.say", "Listen to Say");
        public static readonly LocString ListenSayHelp = new("ride.settings.sayHelp", "Follow flags the conductor posts in Say. Handy when you stand next to the conductor, or when you test the ride with your own flags.");
        public static readonly LocString LateJoin = new("ride.settings.lateJoin", "Late-join limit");
        public static readonly LocString LateJoinHelp = new("ride.settings.lateJoinHelp", "When a ride starts, the conductor's last flag is still followed if it is at most this old. An older flag is skipped and the ride waits for the next one.");
        public static readonly LocString IdleLimit = new("ride.settings.idle", "Wait for the first flag");
        public static readonly LocString IdleLimitHelp = new("ride.settings.idleHelp", "How long the ride waits for the conductor's first flag. When none arrives in this time, the ride ends on its own.");
        public static readonly LocString QuietEnd = new("ride.settings.quietEnd", "End after the conductor goes quiet");
        public static readonly LocString QuietEndHelp = new("ride.settings.quietEndHelp", "Once the ride has reached a flag, it ends on its own when no new flag from the conductor arrives for this long and no fight is going on.");
        public static readonly LocString EndOnPhrase = new("ride.settings.endOnPhrase", "End when the conductor says so");
        public static readonly LocString EndOnPhraseHelp = new("ride.settings.endOnPhraseHelp", "End the ride when the conductor says in Shout, Yell, Say or party chat that the train is over, with words such as \"thanks for coming\" or \"that's all\". A fight in progress is finished first. Only English phrases are recognized for now.");

        public static readonly LocString EngagementGroup = new("ride.settings.engagement", "Engagement");
        public static readonly LocString WaitForPull = new("ride.settings.waitForPull", "Wait for the pull");
        public static readonly LocString WaitForPullHelp = new("ride.settings.waitForPullHelp", "Hold at the flag until someone else has pulled the mark, then join the fight. Off attacks the mark as soon as it is in sight, which makes you the one who pulls it.");
        public static readonly LocString PullWait = new("ride.settings.pullWait", "Pull wait");
        public static readonly LocString PullWaitHelp = new("ride.settings.pullWaitHelp", "How long to wait at a flag for the mark to show up and be pulled. When the time runs out, the ride gives the mark up and waits for the next flag.");
        public static readonly LocString EndWhenAllCredited = new("ride.settings.endWhenAllCredited", "End when every mark is credited");
        public static readonly LocString EndWhenAllCreditedHelp = new("ride.settings.endWhenAllCreditedHelp", "End the ride on its own once as many marks are credited as the expansion has A ranks: 17 in A Realm Reborn, 12 in every later expansion.");
        public static readonly LocString TagWithRanged = new("ride.settings.tagWithRanged", "Tag marks with ranged attacks");
        public static readonly LocString TagWithRangedHelp = new("ride.settings.tagWithRangedHelp", "Attack marks with one weak attack instead of the full rotation, so a mark that melts in seconds lives long enough for the whole train to land a hit. One landed hit is all the credit asks. Tanks and melee jobs use their ranged attack from about 20 yalms; ranged jobs, casters and healers use their basic single-target attack. BossMod still dodges, and anything else that attacks you is fought with the full rotation. When the attack cannot go off for 15 seconds with the mark in range, that mark is fought with the full rotation too. Jobs without such an attack, such as Monk, and characters that have not learned it yet fight marks as usual.");

        public static readonly LocString ChatOpenTrain = new("ride.chat.openTrain", "Open the Train page");

        public static readonly LocString WaitingForTeleport = new("ride.status.waitingForTeleport", "Waiting for a teleport to {0}");
        public static readonly LocString TravelGroup = new("ride.settings.travel", "Travel");
        public static readonly LocString FlagTeleport = new("ride.settings.flagTeleport", "Reaching a flag's zone");
        public static readonly LocString FlagTeleportHelp = new("ride.settings.flagTeleportHelp", "How the ride gets into the zone of a flag posted in another zone.");
        public static readonly LocString TeleportMyselfName = new("ride.settings.teleportMyself", "Teleport myself");
        public static readonly LocString TeleportMyselfDetail = new("ride.settings.teleportMyselfDetail", "The ride teleports itself to the attuned aetheryte best placed for the flag.");
        public static readonly LocString WaitForTeleportName = new("ride.settings.waitForTeleport", "Wait for a teleport");
        public static readonly LocString WaitForTeleportDetail = new("ride.settings.waitForTeleportDetail", "The ride waits until you are in the flag's zone, whether you teleport yourself, take a party member's teleport or another plugin such as Hunt Train Assistant takes you there, then travels on from where you land without teleporting again within the zone.");
        public static readonly LocString TeleportWait = new("ride.settings.teleportWait", "Teleport wait");
        public static readonly LocString TeleportWaitHelp = new("ride.settings.teleportWaitHelp", "How long the ride waits for that teleport before it teleports itself. At 0 s it never does, and gives the flag up after 20 minutes without one. A newer flag ends the wait at once, and the wait does not count against the time the ride allows itself to reach the flag.");
        public static readonly LocString AcceptTeleports = new("ride.settings.acceptTeleports", "Accept party teleports");
        public static readonly LocString AcceptTeleportsHelp = new("ride.settings.acceptTeleportsHelp", "During a ride, accept a party member's teleport offer when it leads into the zone of the flag being followed, or into a hunt zone while no flag is known. An offer that comes during a fight or while the ride waits at its flag is accepted once that is over, if that is within a minute. The ride then travels on from where the party landed. While the ride makes its own way to a flag, no offer is taken, so with \"Teleport myself\" few are; choose \"Wait for a teleport\" to ride on the party's teleports. Offers from outside the party, to anywhere else, on the way to the train or outside a ride are left for you to answer.");
    }
}
