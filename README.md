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
  <em>Hunt trains, ridden for you. Built on Dalamud.</em>
</p>

---

## What it does

Hears a hunt train being called anywhere in your region, gets your character to the start aetheryte on that world, follows the conductor's flags from mark to mark, and lands a hit on every A rank before the crowd deletes it. S-rank relays from the same feed can be run as one-off hunts that travel to the mark and wait for another player to pull.

## Features

- **Rides announced trains**: lists the trains HuntAlerts hears, from your data centers, your region or everywhere, each with a countdown and a Ride button.
- **Reads the real start time**: works out the start from the waits, windows and clock ranges in the announcement, and updates a listed train instead of listing it twice.
- **One-off S-Hunts**: travels to the announced world, zone and instance, stages a safe distance from the S rank, waits for another player to pull, and joins the fight.
- **Hunt details**: world, destination, timing and announcement text for each train or relay, with the map location, Party Finder, a navigation arrow and a one-line chat summary one click away.
- **Train notifications**: a chat line with a link, an optional sound, and the Train page opening by itself for trains you could ride.
- **Auto-join**: off by default; switch it on per expansion to join trains without a click, limited to your allowed data centers.
- **Gets you there**: world change, data center travel and instance switching through Lifestream, then teleport and flight to the start aetheryte.
- **Follows the conductor**: reads map flags from Shout, Yell and Say, lands at the nearest attuned aetheryte, follows instance calls, and knows The Tempest's trap.
- **Rides on the party's teleports**: waits for a party member's teleport into the flag's zone and travels on from where you land, or accepts party teleport offers.
- **Catches up**: picks up a train already in progress along its expansion's usual route until the next flag is heard.
- **Hits, never leads**: dismounts at range, waits for the conductor's pull, lands a hit for credit, and never starts a pull or drags adds into the crowd.
- **Tags marks that melt**: fights marks with one weak ranged attack, so a mark that melts in seconds still lives long enough for the whole train.
- **Dodges like a player**: rolls BossMod's dodge delay and extra room from danger per mark, so riders on the same mark don't dodge in lockstep.
- **Knows when it's over**: ends the ride once every A rank is credited or the conductor goes quiet, then heads home, stays for the next train, or runs the after-ride action.
- **Recovery**: gets back up after a death and rejoins the train; re-paths, jumps, or teleports out when it gets stuck.
- **Auto-repair**: Dark Matter during a lull between flags; Grand Company mender before a data center transfer.
- **Auto-consume**: keeps food and medicine buffs up between flags, HQ first.
- **Pause & resume**: park a ride without losing your progress, auto-pause while you're in a duty, and hold new moves while you type in chat.
- **Train party**: accepts party invites during a ride, can shout for a group or put up a Party Finder listing at the start, and leaves the party when the ride is over.
- **GM alert**: ends the ride when a GM is near, with optional toast, beeps, or custom commands.
- **History**: every train recorded with world, data center, marks credited, time on the rails and seals earned, plus a card tracking each expansion's hunt mount.

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
| `/aht conductor <First Last>[@World]` | Follow this player's flags; alone, shows the current conductor |
| `/aht conductor clear` | Stop following anyone |
| `/aht snooze [<minutes>]` | Suspend auto-join and train notifications for the snooze length in Settings, or the minutes given |
| `/aht snooze off` | Lift the snooze |
| `/aht inject <World> <DT\|EW\|SHB\|Centurio> [<aetheryte>] [i<n>] [+<minutes>] [conductor:<First Last>] [body:<text>]` | Push a made-up train announcement through the HuntAlerts intake, so a ride can be tried with no real train (debug helper) |
| `/aht inject srank <World> <creature name> <x> <y> [aetheryte] [i<n>] [body:text]` | Push a made-up S-rank relay through the HuntAlerts intake (debug helper) |
| `/aht target` | Log targeted NPC's BaseId (debug helper) |
| `/aht goto <territory> <x> <y> <z>` | Travel to a point, `/aht goto stop` cancels (debug helper) |
| `/aht goto <world> [<aetheryte>] [i<n>]` | Travel to a world in your region, and on it to a named aetheryte and instance (debug helper) |
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
