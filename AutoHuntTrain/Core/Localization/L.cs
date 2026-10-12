namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Common
    {
        public static readonly LocString Close = new("common.close", "Close");
        public static readonly LocString Cancel = new("common.cancel", "Cancel");
        public static readonly LocString Clear = new("common.clear", "Clear");
        public static readonly LocString Pause = new("common.pause", "Pause");
        public static readonly LocString Resume = new("common.resume", "Resume");
        public static readonly LocString StopRun = new("common.stopRun", "Stop the run");
        public static readonly LocString Working = new("common.working", "Working…");
        public static readonly LocString DragAdjustHint = new("common.dragAdjustHint", "Drag to adjust · Ctrl+click to type");
        public static readonly LocString NoMatches = new("common.noMatches", "Nothing matches “{0}”.");
    }

    internal static class Shell
    {
        public static readonly LocString NavTrain = new("shell.nav.train", "Train");
        public static readonly LocString NavSettings = new("shell.nav.settings", "Settings");
        public static readonly LocString NavHistory = new("shell.nav.history", "History");
        public static readonly LocString NavPlugins = new("shell.nav.plugins", "Plugins");
        public static readonly LocString NavLog = new("shell.nav.log", "Console");
        public static readonly LocString NavChangelog = new("shell.nav.changelog", "Changelog");
        public static readonly LocString NavAbout = new("shell.nav.about", "About");
        public static readonly LocString StatusRunning = new("shell.status.running", "Riding");
        public static readonly LocString StatusPaused = new("shell.status.paused", "Paused");
        public static readonly LocString StatusFeedReady = new("shell.status.feedReady", "Feed ready");
        public static readonly LocString StatusSetupNeeded = new("shell.status.setupNeeded", "Setup needed");
        public static readonly LocString StatusIdle = new("shell.status.idle", "Idle");
        public static readonly LocString Minimize = new("shell.minimize", "Minimize to the title strip");
        public static readonly LocString Restore = new("shell.restore", "Restore the window");
        public static readonly LocString ResumeBlocked = new("shell.resumeBlocked", "Resumes automatically once you leave the duty");
        public static readonly LocString GreetingMorning = new("shell.greeting.morning", "Good morning");
        public static readonly LocString GreetingAfternoon = new("shell.greeting.afternoon", "Good afternoon");
        public static readonly LocString GreetingEvening = new("shell.greeting.evening", "Good evening");
        public static readonly LocString GreetingNight = new("shell.greeting.night", "Late night");
    }

    internal static class Train
    {
        public static readonly LocString TitleSetupNeeded = new("train.title.setupNeeded", "Setup needed");
        public static readonly LocString DetailSetupNeeded = new("train.detail.setupNeeded", "Install the required plugins before your first ride.");
        public static readonly LocString TitleFeedReady = new("train.title.feedReady", "HuntAlerts is listening");
        public static readonly LocString DetailFeedReady = new("train.detail.feedReady", "Train announcements from the community feed will show up on this page.");
        public static readonly LocString TitleRiding = new("train.title.riding", "Riding");
        public static readonly LocString TitlePaused = new("train.title.paused", "Paused");
        public static readonly LocString DetailPausedInContent = new("train.detail.pausedInContent", "Resumes once you leave the duty");
        public static readonly LocString DetailPausedManual = new("train.detail.pausedManual", "Resume whenever you're ready");
        public static readonly LocString OpenPlugins = new("train.openPlugins", "Open plugins");
        public static readonly LocString NoRidesYet = new("train.noRidesYet", "No rides yet");
        public static readonly LocString StatsAppearHere = new("train.statsAppearHere", "your stats will appear here");
        public static readonly LocString LastRide = new("train.lastRide", "Last ride  ·  {0} marks");
        public static readonly LocString LastRideDetail = new("train.lastRideDetail", "{0}  ·  {1}");
        public static readonly LocString SentenceEnd = new("train.sentence.end", ".");

        public static readonly LocString Upcoming = new("train.upcoming", "Upcoming trains");
        public static readonly LocString UpcomingEmpty = new("train.upcomingEmpty", "No announcements have arrived yet. HuntAlerts provides them as trains are called.");
        public static readonly LocString Ride = new("train.ride", "Ride");

        public static readonly LocString WhenDone = new("train.whenDone", "When the ride ends");
        public static readonly LocString WhenDoneHelp = new("train.whenDoneHelp", "What to do once a ride ends on its own. A manual Stop or a fault never triggers it.");
        public static readonly LocString ReturnHome = new("train.returnHome", "Return home after the ride");
        public static readonly LocString ReturnHomeHelp = new("train.returnHomeHelp", "Travel back to your home world once the ride ends, crossing data centers when the train ran on another one. Off leaves you on the train's world, ready for the next one.");
        public static readonly LocString StayForNext = new("train.stayForNext", "Stay for the next train");
        public static readonly LocString StayForNextHelp = new("train.stayForNextHelp", "With auto-join on, when a train on this data center that auto-join would take starts within 20 minutes, skip the way home and the after-ride action and let auto-join take it.");
        public static readonly LocString AfterStayName = new("train.after.stay.name", "Stay where you are");
        public static readonly LocString AfterStayDetail = new("train.after.stay.detail", "Just stop. You're left standing wherever the last mark fell.");
        public static readonly LocString AfterInnName = new("train.after.inn.name", "Return to the inn");
        public static readonly LocString AfterInnDetail = new("train.after.inn.detail", "Travel to your Grand Company city and enter the inn room.");
        public static readonly LocString AfterLogoutName = new("train.after.logout.name", "Log out to title");
        public static readonly LocString AfterLogoutDetail = new("train.after.logout.detail", "Log out to the title screen.");
        public static readonly LocString AfterCloseName = new("train.after.close.name", "Close the game");
        public static readonly LocString AfterCloseDetail = new("train.after.close.detail", "Close FFXIV entirely (via XIVLauncher's /xlkill).");

        public static readonly LocString Start = new("train.start", "START");
        public static readonly LocString Stop = new("train.stop", "STOP");
        public static readonly LocString PauseCaps = new("train.pause", "PAUSE");
        public static readonly LocString ResumeCaps = new("train.resume", "RESUME");
        public static readonly LocString InContent = new("train.inContent", "in content");
        public static readonly LocString ReasonInstall = new("train.reason.install", "install the required plugins");
        public static readonly LocString StartSub = new("train.startSub", "follow the conductor's flags");
        public static readonly LocString StateRunning = new("train.state.running", "riding");
        public static readonly LocString StatePaused = new("train.state.paused", "paused");
        public static readonly LocString StopSub = new("train.stopSub", "{0} · {1}");
    }

    internal static class Run
    {
        public static readonly LocString PhasePreparing = new("run.phase.preparing", "Getting ready");
        public static readonly LocString PhaseTravelling = new("run.phase.travelling", "Travelling");
        public static readonly LocString PhaseSearching = new("run.phase.searching", "Searching");
        public static readonly LocString PhaseFighting = new("run.phase.fighting", "Fighting");
        public static readonly LocString PhaseFinishing = new("run.phase.finishing", "Finishing up");
        public static readonly LocString PhaseStandingBy = new("run.phase.standingBy", "Standing by");
        public static readonly LocString PhaseReady = new("run.phase.ready", "Ready");
        public static readonly LocString PhasePaused = new("run.phase.paused", "Paused");
        public static readonly LocString PhasePausedInContent = new("run.phase.pausedInContent", "Paused (in content)");
        public static readonly LocString SomewhereElse = new("run.somewhereElse", "Somewhere else");
        public static readonly LocString TileMarks = new("run.tile.marks", "Marks");
        public static readonly LocString TileSeals = new("run.tile.seals", "Seals");
        public static readonly LocString TileElapsed = new("run.tile.elapsed", "Elapsed");
        public static readonly LocString NutsSub = new("run.nutsSub", "+{0} nuts");
    }

    internal static class History
    {
        public static readonly LocString Title = new("history.title", "History");
        public static readonly LocString Empty = new("history.empty", "Your finished rides will show up here.");
        public static readonly LocPlural Summary = new("history.summary", "{0} recorded ride  ·  {1} on the rails  ·  {2} marks/h average", "{0} recorded rides  ·  {1} on the rails  ·  {2} marks/h average");
        public static readonly LocString TileRuns = new("history.tile.runs", "Rides");
        public static readonly LocString TileMarks = new("history.tile.marks", "Marks");
        public static readonly LocString TileSeals = new("history.tile.seals", "Seals");
        public static readonly LocString TileNuts = new("history.tile.nuts", "Nuts");
        public static readonly LocString NoRuns = new("history.noRuns", "No rides recorded yet. Finish (or stop) a ride and it'll show up here.");
        public static readonly LocString MarksPerRun = new("history.marksPerRun", "Marks per ride");
        public static readonly LocString RecentRuns = new("history.recentRuns", "Recent rides");
        public static readonly LocPlural ChartRange = new("history.chartRange", "last {0} ride  ·  oldest to newest", "last {0} rides  ·  oldest to newest");
        public static readonly LocString ChartPeak = new("history.chartPeak", "peak {0}");
        public static readonly LocString ChartTooltip = new("history.chartTooltip", "{0}  ·  {1} marks  ·  {2}");
        public static readonly LocString RowDetail = new("history.rowDetail", "{0}  ·  {1}  ·  {2}");
        public static readonly LocString TooltipRate = new("history.tooltip.rate", "Rate: {0} marks/h");
        public static readonly LocString TooltipWorld = new("history.tooltip.world", "World: {0}  ·  {1}");
        public static readonly LocString JustNow = new("history.time.justNow", "just now");
        public static readonly LocString MinutesAgo = new("history.time.minutesAgo", "{0}m ago");
        public static readonly LocString HoursAgo = new("history.time.hoursAgo", "{0}h ago");
        public static readonly LocString DaysAgo = new("history.time.daysAgo", "{0}d ago");
        public static readonly LocString ClearHistory = new("history.clear", "Clear history");
        public static readonly LocString ClearQuestion = new("history.clearQuestion", "Delete all recorded rides?");
        public static readonly LocString ClearYes = new("history.clearYes", "Yes, clear");
        public static readonly LocString WorldAndDataCenter = new("history.worldDataCenter", "{0} Â· {1}");
        public static readonly LocString TileFinished = new("history.tile.finished", "{0} finished");
        public static readonly LocString TilePerRide = new("history.tile.perRide", "{0} per ride");
        public static readonly LocPlural TileNutsCount = new("history.tile.nutsCount", "{0} nut", "{0} nuts");
        public static readonly LocString TileTopWorld = new("history.tile.topWorld", "Top world");
        public static readonly LocString TooltipCrossed = new("history.tooltip.crossed", "Crossed data centers");
        public static readonly LocString OutcomeAllCredited = new("history.outcome.allCredited", "All credited");
        public static readonly LocString OutcomeConductorQuiet = new("history.outcome.conductorQuiet", "Conductor quiet");
        public static readonly LocString OutcomeStopped = new("history.outcome.stopped", "Stopped");
        public static readonly LocString OutcomeAbandoned = new("history.outcome.abandoned", "Abandoned");
        public static readonly LocString OutcomeFaulted = new("history.outcome.faulted", "Error");
    }

    internal static class Plugins
    {
        public static readonly LocString Title = new("plugins.title", "Plugins");
        public static readonly LocString AllInstalled = new("plugins.allInstalled", "All required plugins are installed and loaded.");
        public static readonly LocPlural Missing = new("plugins.missing", "{0} required plugin is missing.", "{0} required plugins are missing.");
        public static readonly LocString Required = new("plugins.required", "Required");
        public static readonly LocString Optional = new("plugins.optional", "Optional");
        public static readonly LocString Installed = new("plugins.installed", "Installed");
        public static readonly LocString Install = new("plugins.install", "Install");
        public static readonly LocString Installing = new("plugins.installing", "Installing…");
        public static readonly LocString RepoHint = new("plugins.repoHint", "Repo: {0}\nLeft-click to open repo URL · right-click to copy");
        public static readonly LocString Footer = new("plugins.footer",
            "Install adds the plugin's source repository to Dalamud and queues an install. If one-click install fails (URL drift, network), right-click a plugin name to copy its repo URL and add it manually via /xlsettings -> Experimental -> Custom Plugin Repositories.");
        public static readonly LocString PurposeVnavmesh = new("plugins.purpose.vnavmesh", "Pathfinding, flying, and movement to hunt marks.");
        public static readonly LocString PurposeBossMod = new("plugins.purpose.bossMod", "Auto-rotation, targeting, and dodging while fighting marks.");
        public static readonly LocString PurposeLifestream = new("plugins.purpose.lifestream", "World, data center and instance travel to reach the train.");
        public static readonly LocString PurposeHuntAlerts = new("plugins.purpose.huntAlerts", "Hunt train announcements from the community feed.");
    }

    internal static class Log
    {
        public static readonly LocString Title = new("log.title", "Console");
        public static readonly LocPlural Entries = new("log.entries", "{0} line in the buffer", "{0} lines in the buffer");
        public static readonly LocString Empty = new("log.empty", "Nothing logged yet. Start a run and every step the plugin takes shows up here.");
        public static readonly LocString NoMatches = new("log.noMatches", "No lines match the current filters.");
        public static readonly LocString Footer = new("log.footer", "Every line also goes to the Dalamud log (/xllog) with the {0} prefix. When reporting a bug, press Copy log and paste the result into the issue.");
        public static readonly LocString SearchHint = new("log.searchHint", "Search messages and sources");
        public static readonly LocString CopyAll = new("log.copyAll", "Copy log");
        public static readonly LocPlural CopyFiltered = new("log.copyFiltered", "Copy {0} line", "Copy {0} lines");
        public static readonly LocString CopyTooltip = new("log.copyTooltip", "Copies the lines shown below with a header naming the plugin version, Dalamud version and zone, ready to paste into a bug report.");
        public static readonly LocString Copied = new("log.copied", "Copied");
        public static readonly LocPlural CopiedLines = new("log.copiedLines", "Copied {0} line to the clipboard", "Copied {0} lines to the clipboard");
        public static readonly LocString Clear = new("log.clear", "Clear");
        public static readonly LocString ConfirmClear = new("log.confirmClear", "Click again to clear");
        public static readonly LocString Close = new("log.close", "Close");
        public static readonly LocString LevelVerbose = new("log.level.verbose", "Verbose");
        public static readonly LocString LevelDebug = new("log.level.debug", "Debug");
        public static readonly LocString LevelInfo = new("log.level.info", "Info");
        public static readonly LocString LevelWarning = new("log.level.warning", "Warnings");
        public static readonly LocString LevelError = new("log.level.error", "Errors");
        public static readonly LocString LevelTooltip = new("log.levelTooltip", "Click to show or hide these lines. Shift-click to show only this level.");
        public static readonly LocString SourceChip = new("log.sourceChip", "Source: {0}");
        public static readonly LocString SourceChipTooltip = new("log.sourceChipTooltip", "Click to stop filtering by source.");
        public static readonly LocString JumpLatest = new("log.jumpLatest", "Jump to latest");
        public static readonly LocPlural NewLines = new("log.newLines", "{0} new line", "{0} new lines");
        public static readonly LocString Showing = new("log.showing", "Showing {0} of {1}");
        public static readonly LocString ResetFilters = new("log.resetFilters", "Reset filters");
        public static readonly LocString Shortcuts = new("log.shortcuts", "Ctrl+F search · Ctrl+C copy · Shift-click selects a range · Double-click copies a line");
        public static readonly LocPlural Selected = new("log.selected", "{0} line selected", "{0} lines selected");
        public static readonly LocString CopySelection = new("log.copySelection", "Copy selection");
        public static readonly LocString ClearSelection = new("log.clearSelection", "Clear selection");
        public static readonly LocString CopyLine = new("log.copyLine", "Copy line");
        public static readonly LocString CopyToEnd = new("log.copyToEnd", "Copy from here to the end");
        public static readonly LocString OnlySource = new("log.onlySource", "Show only {0}");
        public static readonly LocString Repeated = new("log.repeated", "Repeated {0} times in a row");
        public static readonly LocString HasDetails = new("log.hasDetails", "Has a stack trace. Select the line to read it.");
    }

    internal static class Changelog
    {
        public static readonly LocString Title = new("changelog.title", "What's new");
        public static readonly LocString Subtitle = new("changelog.subtitle", "Every update, newest first.");
        public static readonly LocString Version = new("changelog.version", "Version {0}");
        public static readonly LocString Latest = new("changelog.latest", "Latest");
        public static readonly LocString New = new("changelog.new", "New");
        public static readonly LocPlural Changes = new("changelog.changes", "{0} change", "{0} changes");

        public static readonly LocString[] Release1010 =
        [
            new("changelog.r1010.1", "Added a Buy Me a Coffee button under Patreon on the About page"),
        ];

        public static readonly LocString[] Release1000 =
        [
            new("changelog.r1000.1", "Rides hunt trains announced through HuntAlerts, with a Ride button on every train, auto-join per expansion and snooze"),
            new("changelog.r1000.2", "Travels to the train's world, data center and instance through Lifestream, and carries a ride across the data center relog"),
            new("changelog.r1000.3", "Follows the conductor's map flags from Shout, Yell and Say, picking the conductor from the announcement, the first flag, or by hand"),
            new("changelog.r1000.4", "Waits for the pull and lands a hit on each A rank for credit, never starting a pull itself"),
            new("changelog.r1000.5", "Joins the train's party, returns home after the ride, and records every train in History"),
            new("changelog.r1000.6", "Opens each train's details with Flag on map, Party Finder, Nav and Relay, and catches up with trains that already left their start"),
            new("changelog.r1000.7", "A Humanizer with random reaction delays, so the character is never the first to move"),
        ];
    }

    internal static class About
    {
        public static readonly LocString SupportTitle = new("about.support.title", "Made with love and care");
        public static readonly LocString SupportBody = new("about.support.body", "This plugin is a one-person project, built in my free time because I love this game and its community. Keeping it updated takes a lot of those hours. If it has helped you, supporting me on Patreon or buying me a coffee means I can keep giving it that time. Thank you for being here.");
        public static readonly LocString SupportButton = new("about.support.button", "Support on Patreon");
        public static readonly LocString PatreonHint = new("about.support.hint", "Open Patreon · right-click to copy");
        public static readonly LocString CoffeeButton = new("about.support.coffeeButton", "Buy me a coffee");
        public static readonly LocString CoffeeHint = new("about.support.coffeeHint", "Open Buy Me a Coffee · right-click to copy");
        public static readonly LocString LinkHint = new("about.linkHint", "Click to open · right-click to copy");
        public static readonly LocString MadeBy = new("about.madeBy", "Made by {0}");
        public static readonly LocString Version = new("about.version", "v {0}");
        public static readonly LocString Community = new("about.community", "Community");
        public static readonly LocString DiscordTitle = new("about.discordTitle", "Join the Discord");
        public static readonly LocString DiscordBody = new("about.discordBody", "Get help, report bugs, share ideas and hear about updates first.");
        public static readonly LocString GitHubTitle = new("about.githubTitle", "View on GitHub");
        public static readonly LocString GitHubBody = new("about.githubBody", "Browse the source code and every release.");
        public static readonly LocString ReminderTitle = new("about.reminder.title", "A little reminder");
        public static readonly LocString FactsTitle = new("about.facts.title", "Did you know?");
        public static readonly LocString QuotesTitle = new("about.quotes.title", "Words to live by");
        public static readonly LocString JokesTitle = new("about.jokes.title", "Just for fun");

        public static readonly LocString[] Reminders =
        [
            new("about.reminder.1", "Been at it a while? Roll your shoulders and take one slow breath."),
            new("about.reminder.2", "Hydration check. When did you last drink some water?"),
            new("about.reminder.3", "Blink a few times and let your eyes rest for a moment."),
            new("about.reminder.4", "Stand up, stretch, and shake out your hands. Future you says thanks."),
            new("about.reminder.5", "Sit up and settle in comfortably. Your back will thank you later."),
            new("about.reminder.6", "Remember to eat something today. You matter more than any score."),
            new("about.reminder.7", "Eyes feel tired? Look at something far away for twenty seconds."),
            new("about.reminder.8", "Whatever you're chasing, you're allowed to take a break whenever."),
            new("about.reminder.9", "You're doing great. Be a little kinder to yourself today."),
            new("about.reminder.10", "A glass of water and a quick stretch can reset a long session."),
            new("about.reminder.11", "Unclench your jaw and drop your shoulders. There you go."),
            new("about.reminder.12", "Rest is part of the journey too. Step away whenever you need to."),
        ];

        public static readonly LocString[] Facts =
        [
            new("about.facts.1", "Honey never spoils. Jars over 3,000 years old have been found still edible."),
            new("about.facts.2", "Octopuses have three hearts and blue blood."),
            new("about.facts.3", "A day on Venus is longer than a whole year on Venus."),
            new("about.facts.4", "Bananas are berries, but strawberries aren't."),
            new("about.facts.5", "There are more possible chess games than atoms in the observable universe."),
            new("about.facts.6", "Sharks have been around longer than trees have."),
            new("about.facts.7", "A group of flamingos is called a flamboyance."),
            new("about.facts.8", "Honeybees can recognize individual human faces."),
            new("about.facts.9", "Wombat droppings are cube shaped."),
            new("about.facts.10", "The Eiffel Tower can grow over 15 cm taller on a hot day."),
            new("about.facts.11", "Hot water can sometimes freeze faster than cold water."),
            new("about.facts.12", "A bolt of lightning is roughly five times hotter than the surface of the Sun."),
        ];

        public static readonly LocString[] Quotes =
        [
            new("about.quotes.1", "Done is better than perfect. You can always polish later."),
            new("about.quotes.2", "Small steps every day add up to surprising distances."),
            new("about.quotes.3", "Comparison is the thief of joy. Run your own race."),
            new("about.quotes.4", "Progress, not perfection."),
            new("about.quotes.5", "You don't have to be great to start, but you have to start to be great."),
            new("about.quotes.6", "Be patient with yourself. Growth takes time."),
            new("about.quotes.7", "The best time to begin was yesterday. The second best is right now."),
            new("about.quotes.8", "Celebrate the small wins. They count too."),
            new("about.quotes.9", "Slow progress is still progress."),
            new("about.quotes.10", "Your only real competition is who you were yesterday."),
        ];

        public static readonly LocString[] Jokes =
        [
            new("about.jokes.1", "Why don't scientists trust atoms? Because they make up everything."),
            new("about.jokes.2", "I would tell you a chemistry joke, but I know I wouldn't get a reaction."),
            new("about.jokes.3", "Why did the scarecrow win an award? He was outstanding in his field."),
            new("about.jokes.4", "I'm reading a book about anti-gravity. It's impossible to put down."),
            new("about.jokes.5", "Why don't skeletons fight each other? They don't have the guts."),
            new("about.jokes.6", "What do you call fake spaghetti? An impasta."),
            new("about.jokes.7", "Why did the bicycle fall over? It was two tired."),
            new("about.jokes.8", "What do you call cheese that isn't yours? Nacho cheese."),
            new("about.jokes.9", "I'm on a seafood diet. I see food, and I eat it."),
            new("about.jokes.10", "I only know 25 letters of the alphabet. I don't know y."),
        ];
    }

    internal static class Settings
    {
        public static readonly LocString Title = new("settings.title", "Settings");
        public static readonly LocString Language = new("settings.language", "Language");
        public static readonly LocString LanguageHelp = new("settings.languageHelp", "The language of this plugin's windows. Zone, mark, and world names always follow the game client.");
        public static readonly LocString SearchHint = new("settings.searchHint", "Search...");

        public static readonly LocString CatGeneral = new("settings.cat.general", "General");
        public static readonly LocString CatGeneralSub = new("settings.cat.generalSub", "Window and behavior preferences.");
        public static readonly LocString CatFeedSub = new("settings.cat.feedSub", "Which trains to auto-join, and how to hear about them.");
        public static readonly LocString CatRideSub = new("settings.cat.rideSub", "Following the conductor, fighting the marks, and what happens after.");

        public static readonly LocString GeneralWindow = new("settings.general.window", "Window");
        public static readonly LocString OpenOnLogin = new("settings.general.openOnLogin", "Open on login");
        public static readonly LocString OpenOnLoginHelp = new("settings.general.openOnLoginHelp", "Pop the main window automatically the next time you log in.");
        public static readonly LocString GeneralBehavior = new("settings.general.behavior", "Behavior");
        public static readonly LocString AutoPause = new("settings.general.autoPause", "Auto-pause in content");
        public static readonly LocString AutoPauseHelp = new("settings.general.autoPauseHelp", "Pause the ride while you are inside a duty, trial, raid, or any other instanced content, then resume it once you are back outside. Your ride and session stats are kept.");
        public static readonly LocString PauseTyping = new("settings.general.pauseTyping", "Hold while typing");
        public static readonly LocString PauseTypingHelp = new("settings.general.pauseTypingHelp", "While a text box of the game has focus, like the chat line, the ride starts no new move until you close it. A move already under way carries on, and the latest flag is taken once you are done.");
    }

    internal static class Plugin
    {
        public static readonly LocString CommandHelp = new("plugin.commandHelp", "Toggle the Auto Hunt Train window. /aht config | stats | deps | log | changelog | about | pause (pause or resume the ride) | conductor <First Last>[@World] or conductor clear (whose flags to follow) | snooze [minutes] or snooze off (suspend auto-join) | inject <World> <DT|EW|SHB|Centurio> [aetheryte] [i<n>] [+minutes] [conductor:First Last] [body:text] (inject a test train) | inject srank <World> <creature name> <x> <y> [aetheryte] [i<n>] [body:text] (inject a test S-rank relay) | target (dump the current target's IDs, registry match, HP, combat and pull state) | goto <territory> <x> <y> <z>, goto <world> [aetheryte] [i<n>] or goto home (travel helpers, goto stop cancels).");
        public static readonly LocString CommandHelpAlias = new("plugin.commandHelpAlias", "Alias for /aht.");
    }
}
