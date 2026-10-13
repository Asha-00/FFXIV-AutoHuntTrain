using ECommons.DalamudServices;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using LuminaAction = Lumina.Excel.Sheets.Action;

namespace AutoHuntTrain.Core.Game.Ops;

// A weak attack each job can repeat on one target from range: the ranged attack of a tank or melee job, the basic
// single-target filler of everyone else. One landed hit puts the character on a mark's enemy list, which is all a hunt
// mark's credit asks, so a train that melts its marks still credits everyone who tags them. A job's ladder runs from its
// highest rung down, and each rung's level and teaching quest come from the Action sheet; the ids were checked against
// the sheet of game version 2026.09.15. Pugilist, Monk, Beastmaster and Blue Mage have no single-target attack at range
// and get no ladder.
internal static unsafe class TagActions
{
    // GetActionStatus answers 0 for an action ready to go.
    private const uint ReadyStatus = 0;
    private const uint NoActionManagerStatus = uint.MaxValue;

    // The game's answers for an action with nothing wrong that is waiting out an animation lock, a cast or its recast.
    private const uint CannotUseYetStatus = 572;
    private const uint CastingStatus = 580;
    private const uint NotReadyStatus = 582;

    private const string UseThrottleKey = "AutoHuntTrain.Tag.Use";
    // The game drops actions sent faster than this.
    private const int UseThrottleMs = 500;
    // A ranged weaponskill reads -1 on the sheet: it reaches as far as the job's auto-attack, 25 yalms.
    private const float AutoAttackReachYalms = 25f;

    private const uint Gladiator = 1;
    private const uint Marauder = 3;
    private const uint Lancer = 4;
    private const uint Archer = 5;
    private const uint Conjurer = 6;
    private const uint Thaumaturge = 7;
    private const uint Paladin = 19;
    private const uint Warrior = 21;
    private const uint Dragoon = 22;
    private const uint Bard = 23;
    private const uint WhiteMage = 24;
    private const uint BlackMage = 25;
    private const uint Arcanist = 26;
    private const uint Summoner = 27;
    private const uint Scholar = 28;
    private const uint Rogue = 29;
    private const uint Ninja = 30;
    private const uint Machinist = 31;
    private const uint DarkKnight = 32;
    private const uint Astrologian = 33;
    private const uint Samurai = 34;
    private const uint RedMage = 35;
    private const uint Gunbreaker = 37;
    private const uint Dancer = 38;
    private const uint Reaper = 39;
    private const uint Sage = 40;
    private const uint Viper = 41;
    private const uint Pictomancer = 42;

    private const uint ShieldLob = 24;
    private const uint Tomahawk = 46;
    private const uint Unmend = 3624;
    private const uint LightningShot = 16143;
    private const uint PiercingTalon = 90;
    private const uint ThrowingDagger = 2247;
    private const uint Enpi = 7486;
    private const uint Harpe = 24386;
    private const uint WrithingSnap = 34632;
    private const uint HeavyShot = 97;
    private const uint BurstShot = 16495;
    private const uint SplitShot = 2866;
    private const uint HeatedSplitShot = 7411;
    private const uint Cascade = 15989;
    private const uint Blizzard = 142;
    private const uint Ruin = 163;
    private const uint RuinII = 172;
    private const uint RuinIII = 3579;
    private const uint Jolt = 7503;
    private const uint JoltII = 7524;
    private const uint JoltIII = 37004;
    private const uint FireInRed = 34650;
    private const uint Stone = 119;
    private const uint StoneII = 127;
    private const uint StoneIII = 3568;
    private const uint StoneIV = 7431;
    private const uint Glare = 16533;
    private const uint GlareIII = 25859;
    private const uint ScholarRuin = 17869;
    private const uint Broil = 3584;
    private const uint BroilII = 7435;
    private const uint BroilIII = 16541;
    private const uint BroilIV = 25865;
    private const uint Malefic = 3596;
    private const uint MaleficII = 3598;
    private const uint MaleficIII = 7442;
    private const uint MaleficIV = 16555;
    private const uint FallMalefic = 25871;
    private const uint Dosis = 24283;
    private const uint DosisII = 24306;
    private const uint DosisIII = 24312;

    public static bool HasLadder(uint jobId) => !LadderFor(jobId).IsEmpty;

    // The highest rung the character has: its level reached and, for an action a quest teaches, that quest done.
    public static TagAction Resolve(uint jobId, byte level)
    {
        var ladder = LadderFor(jobId);
        if (ladder.IsEmpty)
        {
            return default;
        }

        var sheet = Svc.Data.GetExcelSheet<LuminaAction>();
        for (var rungIndex = 0; rungIndex < ladder.Length; rungIndex++)
        {
            if (!sheet.TryGetRow(ladder[rungIndex], out var action) || action.ClassJobLevel > level || !IsLearned(action.UnlockLink.RowId))
            {
                continue;
            }

            var reach = action.Range < 0 ? AutoAttackReachYalms : action.Range;
            return new TagAction(action.RowId, reach, action.Name.ExtractText());
        }

        return default;
    }

    // The adjusted id follows the game's own upgrades and combos, as a hotbar slot does. True once the game took the
    // cast; status carries the game's answer either way.
    public static bool TryUse(uint actionId, ulong targetId, out uint status)
    {
        var actions = ActionManager.Instance();
        if (actions is null)
        {
            status = NoActionManagerStatus;
            return false;
        }

        var adjusted = actions->GetAdjustedActionId(actionId);
        status = actions->GetActionStatus(ActionType.Action, adjusted, targetId);
        if (status != ReadyStatus || !EzThrottler.Throttle(UseThrottleKey, UseThrottleMs))
        {
            return false;
        }

        return actions->UseAction(ActionType.Action, adjusted, targetId);
    }

    public static bool IsRoutine(uint status) => status is ReadyStatus or CannotUseYetStatus or CastingStatus or NotReadyStatus;

    private static bool IsLearned(uint unlockLink)
    {
        if (unlockLink == 0)
        {
            return true;
        }

        var state = UIState.Instance();
        return state is not null && state->IsUnlockLinkUnlockedOrQuestCompleted(unlockLink);
    }

    private static ReadOnlySpan<uint> LadderFor(uint jobId) => jobId switch
    {
        Gladiator or Paladin => [ShieldLob],
        Marauder or Warrior => [Tomahawk],
        DarkKnight => [Unmend],
        Gunbreaker => [LightningShot],
        Lancer or Dragoon => [PiercingTalon],
        Rogue or Ninja => [ThrowingDagger],
        Samurai => [Enpi],
        Reaper => [Harpe],
        Viper => [WrithingSnap],
        Archer => [HeavyShot],
        Bard => [BurstShot, HeavyShot],
        Machinist => [HeatedSplitShot, SplitShot],
        Dancer => [Cascade],
        Thaumaturge or BlackMage => [Blizzard],
        Arcanist => [RuinII, Ruin],
        Summoner => [RuinIII, RuinII, Ruin],
        RedMage => [JoltIII, JoltII, Jolt],
        Pictomancer => [FireInRed],
        Conjurer => [StoneII, Stone],
        WhiteMage => [GlareIII, Glare, StoneIV, StoneIII, StoneII, Stone],
        Scholar => [BroilIV, BroilIII, BroilII, Broil, ScholarRuin],
        Astrologian => [FallMalefic, MaleficIV, MaleficIII, MaleficII, Malefic],
        Sage => [DosisIII, DosisII, Dosis],
        _ => [],
    };
}

internal readonly record struct TagAction(uint ActionId, float ReachMeters, string Name)
{
    public bool IsValid => ActionId != 0;
}
