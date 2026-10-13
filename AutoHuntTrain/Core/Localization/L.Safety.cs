namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Safety
    {
        public static readonly LocString Add = new("safety.add", "Add");
        public static readonly LocString Remove = new("safety.remove", "Remove");
        public static readonly LocString Test = new("safety.test", "Test");
        public static readonly LocString Preview = new("safety.preview", "Preview");

        public static readonly LocString CatUpkeep = new("safety.cat.upkeep", "Upkeep");
        public static readonly LocString CatUpkeepSub = new("safety.cat.upkeepSub", "Repair and food during a ride. They run only in a lull while the ride waits for a flag, never on the way to one or during a fight.");
        public static readonly LocString CatParty = new("safety.cat.partyInvites", "Party");
        public static readonly LocString CatPartySub = new("safety.cat.partyInvitesSub", "On a train, party members share the credit on a mark. Join the parties the crowd offers, find one with a shout, and leave it once the ride is over.");
        public static readonly LocString CatGmAlert = new("safety.cat.gmAlert", "GM alert");
        public static readonly LocString CatGmAlertSub = new("safety.cat.gmAlertSub", "Detects nearby Game Masters and reacts: stop the ride, ping you, or take more drastic action.");

        public static readonly LocString UpkeepTiming = new("safety.upkeep.timing", "Timing");
        public static readonly LocString UpkeepQuiet = new("safety.upkeep.quiet", "Quiet time before upkeep");
        public static readonly LocString UpkeepQuietHelp = new("safety.upkeep.quietHelp", "Repair and food wait until the ride has been waiting this long without a new flag, so they never hold you back when the conductor moves on. A flag that arrives meanwhile stops them. The upkeep before a data center transfer does not wait.");

        public static readonly LocString RepairTrigger = new("safety.repair.trigger", "Repair trigger");
        public static readonly LocString AutoRepair = new("safety.repair.autoRepair", "Auto-repair gear");
        public static readonly LocString AutoRepairHelp = new("safety.repair.autoRepairHelp", "When the lowest equipped item drops to or below the threshold, the plugin repairs in a lull between flags and before a data center transfer. During a ride only Dark Matter from your bag is used; a trip to a repair NPC waits for the upkeep before a transfer. At 0% the gear stops working, so keep some margin.");
        public static readonly LocString AutoRepairOff = new("safety.repair.autoRepairOff", "Auto-repair is off. Enable it to configure repair.");
        public static readonly LocString RepairThreshold = new("safety.repair.threshold", "Repair threshold");
        public static readonly LocString RepairThresholdHelp = new("safety.repair.thresholdHelp", "Trips when the worst equipped slot reaches this condition percentage. 20% leaves comfortable margin before the 0% breakdown.");
        public static readonly LocString RepairSource = new("safety.repair.source", "Repair source");
        public static readonly LocString RepairSourceHelp = new("safety.repair.sourceHelp", "How the repair is performed. Self-repair uses Dark Matter from your bag (no travel). NPC repair travels to your Grand Company mender.");
        public static readonly LocString RepairSelfThenNpcName = new("safety.repair.selfThenNpc.name", "Self, then NPC");
        public static readonly LocString RepairSelfThenNpcDetail = new("safety.repair.selfThenNpc.detail", "Use Dark Matter from your bag first; fall back to the Grand Company mender when you run out.");
        public static readonly LocString RepairSelfOnlyName = new("safety.repair.selfOnly.name", "Self only");
        public static readonly LocString RepairSelfOnlyDetail = new("safety.repair.selfOnly.detail", "Repair with Dark Matter from your bag. No travel.");
        public static readonly LocString RepairNpcOnlyName = new("safety.repair.npcOnly.name", "NPC only");
        public static readonly LocString RepairNpcOnlyDetail = new("safety.repair.npcOnly.detail", "Travel to your Grand Company mender (or a custom NPC) and repair there.");
        public static readonly LocString CustomNpc = new("safety.repair.customNpc", "Custom repair NPC");
        public static readonly LocString CustomNpcHelp = new("safety.repair.customNpcHelp", "Optional. Travel to any repair NPC instead of the Grand Company mender. Target the NPC in-game, then click \"Set from target\". Clear to fall back to the GC mender.");
        public static readonly LocString NpcNote = new("safety.repair.npcNote", "NPC repair uses your custom repair NPC if set, otherwise your Grand Company mender, teleporting there when needed. A custom NPC removes the Grand Company requirement.");
        public static readonly LocString NpcSet = new("safety.repair.npcSet", "{0}  ({1})");
        public static readonly LocString NpcNone = new("safety.repair.npcNone", "None: the Grand Company mender is used.");
        public static readonly LocString SetFromTarget = new("safety.repair.setFromTarget", "Set from target");
        public static readonly LocString NoTargetChat = new("safety.repair.noTargetChat", "No target. Target a repair NPC first, then click again.");
        public static readonly LocString NpcSetChat = new("safety.repair.npcSetChat", "Custom repair NPC set: {0} ({1}).");

        public static readonly LocString ConsumablesGroup = new("safety.consumables.group", "Consumables");
        public static readonly LocString AutoConsume = new("safety.consumables.autoConsume", "Auto-consume food & medicine");
        public static readonly LocString AutoConsumeHelp = new("safety.consumables.autoConsumeHelp", "Use food and medicine in a lull between flags to keep their buffs up; Well Fed alone is a free +3% EXP. Items are consumed only out of combat, and refreshed before the buff runs out.");
        public static readonly LocString AutoConsumeOff = new("safety.consumables.autoConsumeOff", "Auto-consume is off. Enable it to pick items.");
        public static readonly LocString RefreshUnder = new("safety.consumables.refreshUnder", "Refresh when under");
        public static readonly LocString RefreshUnderHelp = new("safety.consumables.refreshUnderHelp", "Re-consume once the buff has fewer than this many minutes left. 0 only re-applies after it fully wears off. A meal lasts 30 minutes.");
        public static readonly LocString RefreshWornOff = new("safety.consumables.refreshWornOff", "only when worn off");
        public static readonly LocString RefreshFormat = new("safety.consumables.refreshFormat", "%d min left");
        public static readonly LocString ConsumablesItems = new("safety.consumables.items", "Items");
        public static readonly LocString AddItem = new("safety.consumables.addItem", "Add an item");
        public static readonly LocString AddItemHelp = new("safety.consumables.addItemHelp", "Pick from the food and medicine in your bag. HQ is used automatically when you have it.");
        public static readonly LocString ActiveItems = new("safety.consumables.activeItems", "Active items");
        public static readonly LocString ActiveItemsHelp = new("safety.consumables.activeItemsHelp", "Each is kept active in order; the next available one is consumed if the first runs out.");
        public static readonly LocString NoneInBag = new("safety.consumables.noneInBag", "No food or medicine in your bag. Stock some, then add it here.");
        public static readonly LocString KindFood = new("safety.consumables.kindFood", "Food");
        public static readonly LocString KindMedicine = new("safety.consumables.kindMedicine", "Medicine");
        public static readonly LocString Added = new("safety.consumables.added", "(added)");
        public static readonly LocString ItemLabel = new("safety.consumables.itemLabel", "{0}  [{1}]{2}");
        public static readonly LocString AlreadyAdded = new("safety.consumables.alreadyAdded", "Already added.");
        public static readonly LocString NoItemsAdded = new("safety.consumables.noItemsAdded", "No items added, so nothing will be consumed.");
        public static readonly LocString WellFed = new("safety.consumables.wellFed", "Well Fed");
        public static readonly LocString Medicated = new("safety.consumables.medicated", "Medicated");
        public static readonly LocString NoneInBagShort = new("safety.consumables.noneInBagShort", "{0}, none in bag");

        public static readonly LocString MinutesFormat = new("safety.minutesFormat", "%d min");
        public static readonly LocString SecondsFormat = new("safety.secondsFormat", "%d s");

        public static readonly LocString PartyGroup = new("safety.party.group", "Train party");
        public static readonly LocString AcceptInvites = new("safety.party.accept", "Accept party invites");
        public static readonly LocString AcceptInvitesHelp = new("safety.party.acceptHelp", "While a ride is running, accept party invites on their own. Nothing is accepted while you are already in a party, while the ride is paused, or while a data center transfer is pending, and invites outside a ride are left to you.");
        public static readonly LocString LookingForGroup = new("safety.party.lfg", "Post looking for group");
        public static readonly LocString LookingForGroupHelp = new("safety.party.lfgHelp", "Once per ride, after arriving at the train's start and while not in a party, send one line in Shout asking for a party.");
        public static readonly LocString LookingForGroupText = new("safety.party.lfgText", "Shout text");
        public static readonly LocString LookingForGroupTextHelp = new("safety.party.lfgTextHelp", "The line sent in Shout. Keep it short; an empty text sends nothing.");
        public static readonly LocString LeaveParty = new("safety.party.leave", "Leave the party after the ride");
        public static readonly LocString LeavePartyHelp = new("safety.party.leaveHelp", "When a ride ends on its own, leave the party before the way home. A party you were in before the ride started is kept. Before a data center transfer the party is always left, since the game refuses the transfer otherwise.");
        public static readonly LocString PartyFinderListing = new("safety.party.partyFinder", "Create a Party Finder listing");
        public static readonly LocString PartyFinderListingHelp = new("safety.party.partyFinderHelp", "Once per ride, after arriving at the train's start and while not in a party, put up a public listing in the Party Finder's The Hunt category so other riders can join you. Your other recruitment settings are kept from your last listing. The listing is ended when the ride ends or is stopped, and the game ends it once the party is full; a ride stopped because a GM appeared leaves it up for you to end. While it is up, players can join only through the listing, not by invite.");
        public static readonly LocString PartyFinderComment = new("safety.party.partyFinderComment", "Listing comment");
        public static readonly LocString PartyFinderCommentHelp = new("safety.party.partyFinderCommentHelp", "The comment shown on the listing. The box stops at the length the game accepts; an empty comment lists with none.");
        public static readonly LocString PartyFinderFailedChat = new("safety.party.partyFinderFailedChat", "The Party Finder listing could not be put up; the ride goes on. The log has the details.");
        public static readonly LocString PartyFinderStillUpChat = new("safety.party.partyFinderStillUpChat", "Your Party Finder listing is still up; end it from the Party Finder.");

        public static readonly LocString GmAlerts = new("safety.gm.alerts", "Alerts");
        public static readonly LocString GmStopRun = new("safety.gm.stopRun", "Stop the ride");
        public static readonly LocString GmStopRunHelp = new("safety.gm.stopRunHelp", "End the ride at once when a GM appears nearby. It is recorded as stopped, and nothing runs after it: no way home and no after-ride action. Strongly recommended; the other alerts are little use if the ride keeps going.");
        public static readonly LocString GmToast = new("safety.gm.toast", "Toast notification");
        public static readonly LocString GmToastHelp = new("safety.gm.toastHelp", "Pop a Dalamud toast: \"GM <name> is nearby!\"");
        public static readonly LocString GmChat = new("safety.gm.chat", "Chat alert");
        public static readonly LocString GmChatHelp = new("safety.gm.chatHelp", "Print a red chat warning into your local log.");
        public static readonly LocString GmSound = new("safety.gm.sound", "Sound beeps");
        public static readonly LocString GmSoundHelp = new("safety.gm.soundHelp", "Plays a series of system beeps through your speakers. Loud enough to grab your attention if you're tabbed away.");
        public static readonly LocString BeepCount = new("safety.gm.beepCount", "Beep count");
        public static readonly LocString BeepCountHelp = new("safety.gm.beepCountHelp", "How many beeps to play in the burst.");
        public static readonly LocString BeepCountFormat = new("safety.gm.beepCountFormat", "%d beeps");
        public static readonly LocString BeepLength = new("safety.gm.beepLength", "Beep length");
        public static readonly LocString BeepLengthHelp = new("safety.gm.beepLengthHelp", "How long each beep lasts.");
        public static readonly LocString BeepLengthFormat = new("safety.gm.beepLengthFormat", "%d ms each");
        public static readonly LocString BeepPitch = new("safety.gm.beepPitch", "Beep pitch");
        public static readonly LocString BeepPitchHelp = new("safety.gm.beepPitchHelp", "Tone frequency of each beep.");
        public static readonly LocString BeepPitchFormat = new("safety.gm.beepPitchFormat", "%d Hz");
        public static readonly LocString GmActions = new("safety.gm.actions", "Actions");
        public static readonly LocString GmCommands = new("safety.gm.commands", "Custom commands");
        public static readonly LocString GmCommandsHelp = new("safety.gm.commandsHelp", "Chat commands to run when a GM is spotted. Useful for things like /logout, /sh stay calm, or a macro.");
        public static readonly LocString GmKill = new("safety.gm.kill", "Kill the game");
        public static readonly LocString GmKillHelp = new("safety.gm.killHelp", "Hard-terminate the game process via /xlkill. The last-resort option; no goodbyes, no cutscene, no logout. You'll get a disconnect.");
        public static readonly LocString NoCommands = new("safety.gm.noCommands", "No commands queued.");
    }
}
