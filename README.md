<p align="center">
  <img src="AutoHuntTrain/Images/Icon.png" width="180" alt="Auto Hunt Train icon" />
</p>

<h1 align="center">Auto Hunt Train</h1>

<p align="center">
  <a href="https://discord.gg/hppkAvdBEE"><img alt="Discord" src="https://img.shields.io/badge/Discord-join-5865F2?style=flat-square&logo=discord&logoColor=white"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-AutoHuntTrain/releases/latest"><img alt="Release" src="https://img.shields.io/github/v/release/XeldarAlz/FFXIV-AutoHuntTrain?style=flat-square&color=blue"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-AutoHuntTrain/releases"><img alt="Downloads" src="https://img.shields.io/github/downloads/XeldarAlz/FFXIV-AutoHuntTrain/total?style=flat-square&color=blue&cacheSeconds=300"></a>
  <a href="https://github.com/XeldarAlz/FFXIV-AutoHuntTrain/actions/workflows/release.yml"><img alt="Build" src="https://img.shields.io/github/actions/workflow/status/XeldarAlz/FFXIV-AutoHuntTrain/release.yml?style=flat-square"></a>
  <a href="LICENSE.md"><img alt="License" src="https://img.shields.io/badge/license-AGPL--3.0--or--later-blue?style=flat-square"></a>
</p>

<p align="center">
  <em>Hunt trains and one-off S-rank hunts, handled for you. Built on Dalamud.</em>
</p>

---

## What it does

Hears a hunt train being called anywhere in your region, gets your character to the start aetheryte on that world, follows the conductor's flags from mark to mark, and lands a hit on every A rank before the crowd deletes it.

S-rank relays appear in the same feed as one-off hunts that travel safely to the live mark and wait for another player to pull.

## Features

- **Rides announced trains**: HuntAlerts detects the trains; this plugin lists them from every region under a view you pick (My data centers, My region or Everywhere), each with a countdown, a region tag when it runs outside yours, and a Ride button that takes any train in your region, on any data center, and says why it is off when it cannot.
- **Reads the real start time**: a start given as a wait, such as "starting in 10-15 minutes", "in about 5 min", "~15m" or "in half an hour", is counted from when the train was posted. For a window, or a clock range such as "7:45 PM - 8:00 PM", the ride gets ready for the earlier end and waits until the later one. A post with no start at all gets 15 minutes' grace before the train counts as in progress, so the ride goes to the start zone instead of guessing where the train has got to, and a later post that gives the start updates the listed train instead of listing it twice.
- **Runs one-off S-Hunts**: S-rank relays appear alongside trains with an S-Hunt button. The plugin travels to the announced world, zone and instance, locates the exact live S rank, aims for a safe staging point about 30 yalms from the mark, dismounts, waits for another player to pull, joins the fight and ends after credit is recorded. When the S rank is not in view on arrival, the plugin gives it a few seconds to load, then sweeps toward the reported point, or through the S rank's known spawn points when the relay has no coordinates, until it comes into view. S-Hunts can visit another world on the current data center, but do not travel across data centers.
- **Hunt details**: click a train or S-rank relay for its world, destination, timing and cleaned announcement text. Train details include the conductor and scheduled start; S-rank details include the exact creature and report time. Available actions include starting the ride or S-Hunt, opening the reported map location when coordinates are available, opening the Party Finder, setting an on-screen navigation arrow, and relaying a one-line summary to chat.
- **Train notifications**: a chat line with a link to the train's details and an optional game sound, and, for a train you could ride on your allowed data centers, the Train page opening by itself and the taskbar flashing while the game is in the background. Switch HuntAlerts' own chat alert and banner off to avoid doubles.
- **Auto-join**: off by default; switch it on per expansion to have trains joined without a click, limited to your allowed data centers, with or without data center transfers, and with the lead time a train on another data center needs before auto-join commits. A GM alert switches it off.
- **Gets you there**: world change, data center travel and instance switching through Lifestream, then the plugin's own teleport and flight to the start aetheryte.
- **Follows the conductor**: reads the conductor's map flags from Shout, Yell and Say, and lands at the attuned aetheryte nearest each flag by ground distance, or flies when the flag is close. It knows The Tempest's trap: The Macarenses Angle lies about 400 m below the zone's hunting grounds, so the ride lands at The Ondo Cups unless the flag is on the western ledge beside it.
- **Follows the conductor's instance**: a flag's map link shows which instance it was posted from, and when the teleport lands you in another one, the ride switches to the right one before heading to the flag. An instance written in the flag's text, such as i2 or instance 2, still comes first. Instance calls on a line of their own, such as "swap to 2", "move to instance 2" or "i2", are followed while waiting for the next flag, never in the middle of a fight, and with no aetheryte in sight to switch at, the ride teleports to the nearest attuned one first.
- **Rides on the party's teleports**: set "Reaching a flag's zone" under Settings, Ride, Travel to wait for a teleport, and the ride holds until you are in the flag's zone by any means (your own teleport, a party member's, or Hunt Train Assistant's), then travels on from where you land without teleporting again. After the teleport wait, 60 seconds by default, it teleports itself; at 0 it never does. Accept party teleports takes a party member's teleport offer during a ride when it leads to the flag's zone, or to a hunt zone while no flag is known. Offers that come during a fight or while the ride waits at its flag are accepted once that is over, and offers from outside the party are left to you.
- **Finds the conductor**: from the announcement when it names one, from the first flag posted in the start zone, or picked by hand.
- **Catches up with a train in progress**: a train more than three minutes past its start counts as in progress. While it may only just have set off, the ride listens in its start zone first; after that it estimates how far along its expansion's usual route the train has got, teleports there and listens for the next flag, moving on a zone at a time until it hears one.
- **Hits, never leads**: dismounts at range, waits for the conductor's pull, lands a hit on the zone's A rank for credit, and never starts a pull or drags adds into the crowd.
- **Tags marks that melt**: with Tag marks with ranged attacks (Settings, Ride, Engagement), marks are fought with one weak attack instead of the full rotation, so a mark that melts in seconds still lives long enough for the whole train to land the one hit its credit needs. Tanks and melee jobs throw their ranged attack from about 20 yalms, and ranged jobs, casters and healers use their basic single-target attack. BossMod still dodges, and anything else that attacks you gets the full rotation. Jobs without such an attack, such as Monk, characters that have not learned it yet, and any mark the attack cannot hit for 15 seconds are fought as usual.
- **Dodges like a player**: BossMod steps out of an AoE the instant it is telegraphed and stops right at its edge, so everyone on a mark who runs this plugin would dodge on the same frame to the same spot. With Vary dodging in combat (Settings, Humanizer, Combat movement), each mark rolls a dodge delay, a movement delay and a preference for extra room from danger, all from ranges you choose. The values are temporary overrides on the bundled combat preset, taken back when the mark ends, when the ride stops or pauses, and when the plugin unloads.
- **Knows when it's over**: ends the ride once every A rank of the expansion is credited, or when the conductor has gone quiet. Neither the wait for the first flag nor the quiet time runs out before the train's expected start.
- **Way home**: return to your home world after the train, stay for the next one, or run the after-ride action.
- **Recovery**: gets back up after a death and rejoins the train; re-paths, jumps, or teleports out when it gets stuck.
- **Auto-repair**: Dark Matter during a lull between flags; the Grand Company mender only before a data center transfer.
- **Auto-consume**: keeps food and medicine buffs up in the lulls between flags, HQ first.
- **Pause & resume**: park a ride without losing your progress, auto-pause while you're in a duty, and hold new moves while you type in chat.
- **Train party**: accepts party invites during a ride so the party shares mark credit, can shout for a group at the start, and leaves the party when the ride is over.
- **Party Finder listing**: off by default under Settings, Party. Once per ride, after arriving at the train's start and while you are not in a party, the ride puts up a public listing in The Hunt with your own comment, so other riders can join with one click. It goes through the game's own Party Finder windows after a short human-like pause, never while you are typing, fighting or in a duty, and never holds up the ride: a flag that arrives first simply postpones it. The listing is ended when the ride ends or is stopped, before the party is left, and the game ends it by itself once the party is full. A listing you put up yourself is never touched, and your other Party Finder settings, such as languages, are kept from your last listing.
- **GM alert**: ends the ride when a GM is near, with optional toast, beeps, or custom commands.
- **History**: every train recorded with world, data center, marks credited, time on the rails, and seals earned.
- **Hunt mounts**: a card on the History page shows how close you are to each expansion's hunt mount (Centurio Tiger, Triceratops, Victor and Ullr), with your rank A and rank S kill counts toward its achievement, read from your achievements in the game, and a Complete badge once the mount is earned. The counts refresh while the page is open, at most every five minutes, and shortly after each ride ends.

## Install

In-game: `/xlsettings` → **Experimental** → paste into **Custom Plugin Repositories**:

```
https://raw.githubusercontent.com/XeldarAlz/DalamudPlugins/main/repo.json
```

Tick **Enabled**, click **+**, then **Save and Close**. Open `/xlplugins` → **All Plugins**, search for **Auto Hunt Train**, and install.

The plugin needs a few helpers to be installed and loaded: movement, combat, travel through Lifestream, and HuntAlerts for the train announcements. Open `/aht deps` after install to see the list and one-click each missing one. Data center rides also need data center travel enabled in Lifestream's own settings.

## Commands

| Command | Action |
|---|---|
| `/aht` | Toggle the main window |
| `/hunttrain` | Alias for `/aht` |
| `/aht config` | Open the Settings page |
| `/aht stats` | Open the History page |
| `/aht deps` | Open the Plugins page |
| `/aht log` | Open the Console page |
| `/aht changelog` | Open the Changelog page |
| `/aht about` | Open the About page |
| `/aht pause` | Pause or resume the current ride |
| `/aht conductor <First Last>[@World]` | Follow this player's flags; the world defaults to yours. Alone, shows the current conductor |
| `/aht conductor clear` | Stop following anyone |
| `/aht snooze [<minutes>]` | Suspend auto-join and the train notifications for the snooze length set in Settings, or for the minutes given |
| `/aht snooze off` | Lift the snooze |
| `/aht inject <World> <DT\|EW\|SHB\|Centurio> [<aetheryte>] [i<n>] [+<minutes>] [conductor:<First Last>] [body:<text>]` | Push a made-up train announcement through the same intake as HuntAlerts, on a world in any region, so the rules, the details and the whole ride can be tried with no real train, e.g. `/aht inject Raiden DT Wachunpelo +5 conductor:Pandora Rainfall`. Everything after `body:` is the Discord post, with `\n` for a line break (debug helper) |
| `/aht inject srank <World> <creature name> <x> <y> [aetheryte] [i<n>] [body:text]` | Push a test S-rank relay through the same intake as HuntAlerts so its feed row, details and one-off hunt can be tested without a live relay. Coordinates are the reported map location; `i<n>` supplies an optional instance. This is a debug helper. |
| `/aht target` | Log targeted NPC's BaseId (debug helper) |
| `/aht goto <territory> <x> <y> <z>` | Travel to a point, `/aht goto stop` cancels (debug helper) |
| `/aht goto <world> [<aetheryte>] [i<n>]` | Travel to a world in your region, and on it to a named aetheryte and instance, e.g. `/aht goto Zalera Wachunpelo i2` (debug helper) |
| `/aht goto home` | Travel back to your home world (debug helper) |

## Languages

The windows are available in English, Deutsch, Français, Español, Português (Brasil), Русский, Türkçe, 日本語, and 中文. The plugin picks a language from your Dalamud and game client settings on first launch; change it any time under Settings, General, Language. Game data such as zone, mark, and world names always follows the game client.

Spotted a wrong or awkward translation? Open a [translation issue](https://github.com/XeldarAlz/FFXIV-AutoHuntTrain/issues/new?template=translation_report.yml) and tell me what it should say instead.

## Community

Questions, ideas, or just want to hang out with other players? Come say hi on Discord.

→ [Join our Discord](https://discord.gg/hppkAvdBEE)

## More from me

If you liked this plugin, take a look at my other Dalamud work. You might find something else there for you.

→ [XeldarAlz Dalamud Plugins](https://github.com/XeldarAlz/DalamudPlugins)

## License

AGPL-3.0-or-later. See [LICENSE.md](LICENSE.md). [NOTICE](NOTICE) adds the attribution terms the AGPL allows: a fork, or any project that reuses this code, must credit the original author and must not pass itself off as the original. The license covers the code, not the name or the icon: read the [trademark and naming policy](TRADEMARK.md) before you publish a fork.

How AI is used to build this plugin is written down in [AI usage](AI-USAGE.md).
