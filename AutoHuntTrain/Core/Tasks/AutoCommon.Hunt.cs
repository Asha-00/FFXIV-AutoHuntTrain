using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

public abstract partial class AutoCommon
{
    // A ride wants one credited kill from each mark.
    private const int KillsPerMark = 1;
    private const int RankBSearchBudgetMs = 600_000;
    // An A or S rank is a single roamer that stays down for hours once it falls, so its search gets the longer budget.
    private const int RankASearchBudgetMs = 1_200_000;
    // Two laps over a mark's few known points tell whether it is up.
    private const int MarkSearchLaps = 2;
    // An S rank, or a mark that spawns anywhere in its expansion, is up only after an in-game trigger, so one look at each
    // known point settles it.
    private const int TriggeredMarkSearchLaps = 1;
    // Spawn points are approximate, and a mark near one is in view long before the point itself.
    private const float MarkSweepArriveMeters = 20f;
    private const int MarkScanIntervalMs = 250;
    private const int MarkPointSettleMs = 1_500;
    private const int MaxMarkSightingsPerPoint = 6;
    private const int MaxMarkKnockouts = 3;
    private const int MaxUncountedMarkKills = 3;
    private const float MarkHeightSearchHalfExtentMeters = 5f;
    private const float MarkHeightFallbackHalfExtentMeters = 10f;
    private const float MarkHeightFallbackVerticalMeters = 300f;
    // Height hints are stored in 10 yalm steps, so the floor search starts one step above the hint.
    private const float MarkHeightHintLiftMeters = 10f;
    private const int MaxMarkPointsOnStack = 64;

    private enum MarkLeg { Arrived, Sighted, Failed }

    private HuntPhase markPhase = HuntPhase.Idle;

    internal HuntPhase MarkPhase
    {
        get => markPhase;
        private set
        {
            if (markPhase == value)
            {
                return;
            }

            markPhase = value;
            OnMarkPhaseChanged(value);
        }
    }

    // Lets a ride surface the hunt's phase the moment it changes instead of polling for it.
    private protected virtual void OnMarkPhaseChanged(HuntPhase phase)
    {
    }

    // The one call a ride makes per mark. A hunt mark credits everyone who fights it, so claimed copies are fought too,
    // and the kill ledger, not a counter of the game's, says when the kill counted.
    protected async Task<MarkOutcome> HuntTrainMark(TrainMark mark)
    {
        var name = HuntMarkRegistry.NameOf(mark.NameId);
        var where = mark.HasFlag ? $"flagged at {FormatPosition(mark.FlaggedPosition)}" : "no flag";
        Diag($"Hunt: {name} (name {mark.NameId}, territory {mark.TerritoryId}, {where}, {ConditionTag()})");
        if (!CombatAnswering())
        {
            return MarkOutcome.CombatUnavailable;
        }

        var hunt = CreateTrainHunt(mark, name);
        if (hunt is null)
        {
            Warn($"Hunt: no flag and no spawn points are known for {name}; skipping it");
            return MarkOutcome.Unsupported;
        }

        if (hunt.MarkRank is { } rank)
        {
            var trigger = hunt.AppearsOnTrigger ? " that appears only after an in-game trigger" : string.Empty;
            Diag($"Hunt: {name} is a rank {rank} hunt mark{trigger}; claimed copies are fought too, over up to {hunt.SearchLaps} lap(s) of {hunt.SpawnPoints.Length} point(s) within {hunt.SearchBudgetMs / TimeUnits.MillisecondsPerSecond}s");
        }

        return await RunHunt(hunt);
    }

    // The call a ride makes for a mark it already sees at the conductor's flag: the fight starts on that copy and no
    // spawn point is ever searched, because a mark that leaves a flag has fallen to the train.
    private protected async Task<MarkOutcome> HuntSightedMark(uint nameId, uint territoryId, MarkSighting sighting)
    {
        var name = HuntMarkRegistry.NameOf(nameId);
        Diag($"Hunt: {name} (name {nameId}, territory {territoryId}) in sight {sighting.DistanceToHitbox:F0}m away at {FormatPosition(sighting.Position)}, {sighting.CurrentHp} hp ({ConditionTag()})");
        if (!CombatAnswering())
        {
            return MarkOutcome.CombatUnavailable;
        }

        return await RunHunt(MarkHuntContext.ForSightedMark(nameId, name, territoryId));
    }

    private bool CombatAnswering()
    {
        if (BossModIPC.Instance.IsAvailable)
        {
            return true;
        }

        Warn("Hunt: the combat plugin is not answering, so marks cannot be fought");
        return false;
    }

    // The ledger follows every live copy of the mark for the whole hunt, so one that falls while the character is still
    // on its way, with our part in the fight, is credited too.
    private async Task<MarkOutcome> RunHunt(MarkHuntContext hunt)
    {
        EnsureHuntCombatPreset();
        HoldCombatMovement("hunt");
        var ledger = Plugin.Kills;
        void OnCredited(uint nameId)
        {
            if (nameId == hunt.NameId)
            {
                hunt.CreditedKills++;
            }
        }

        ledger.SetInterest([hunt.NameId]);
        ledger.Credited += OnCredited;
        var startedAt = Environment.TickCount64;
        var outcome = MarkOutcome.Cancelled;
        try
        {
            outcome = await RunMarkHunt(hunt);
            return outcome;
        }
        finally
        {
            ledger.Credited -= OnCredited;
            ledger.ClearTracking();
            BossModIPC.Instance.ClearActive();
            MarkPhase = HuntPhase.Idle;
            Diag($"Hunt: {hunt.Name} ended {outcome} with {hunt.CreditedKills} kill(s) credited after {(Environment.TickCount64 - startedAt) / TimeUnits.MillisecondsPerSecond}s");
        }
    }

    private async Task<MarkOutcome> RunMarkHunt(MarkHuntContext hunt)
    {
        while (true)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return MarkOutcome.Cancelled;
            }

            var outcome = IsMarkKnockedOut() ? MarkOutcome.Died : await FindAndFightMark(hunt);
            if (outcome != MarkOutcome.Died)
            {
                return outcome;
            }

            hunt.Knockouts++;
            if (hunt.FightsSightedOnly)
            {
                // By the time the character stands again the train has moved on, so the mark is not returned to.
                Diag($"Hunt: knocked out fighting {hunt.Name} at the flag; recovering and leaving it");
                await RecoverFromMarkKnockout();
                return MarkOutcome.Died;
            }

            if (hunt.Knockouts >= MaxMarkKnockouts)
            {
                Warn($"Hunt: knocked out {hunt.Knockouts} times hunting {hunt.Name}; giving it up");
                return MarkOutcome.Died;
            }

            Diag($"Hunt: knocked out {hunt.Knockouts}/{MaxMarkKnockouts} hunting {hunt.Name}; recovering and heading back");
            if (!await RecoverFromMarkKnockout())
            {
                return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.Died;
            }
        }
    }

    private Task<MarkOutcome> FindAndFightMark(MarkHuntContext hunt)
        => hunt.FightsSightedOnly ? EngageSightedMark(hunt) : SearchForMark(hunt);

    // Fights the copy in view until the kill counts, the mark is gone, or the fight budget runs out; nothing is searched.
    private async Task<MarkOutcome> EngageSightedMark(MarkHuntContext hunt)
    {
        hunt.StartClock(hunt.FightBudgetMs, MarkOutcome.Unreachable);
        if (await FightVisibleMarks(hunt) is { } outcome)
        {
            return outcome;
        }

        Diag($"Hunt: {hunt.Name} is no longer in view at the flag");
        return CheckMarkStanding(hunt) ?? MarkOutcome.NotFound;
    }

    // The flag comes first, then every spawn point of the mark's rank in its zone that the flag does not already stand on.
    private static MarkHuntContext? CreateTrainHunt(in TrainMark mark, string name)
    {
        var territoryId = mark.TerritoryId != 0 ? mark.TerritoryId : HuntMarkRegistry.SpawnTerritoryOf(mark.NameId);
        if (territoryId == 0)
        {
            return null;
        }

        Vector3[] flagged = mark.HasFlag ? [mark.FlaggedPosition] : [];
        var points = HuntSpawns.Merge(mark.NameId, territoryId, flagged);
        return points.Length == 0 ? null : MarkHuntContext.ForTrainMark(mark.NameId, name, territoryId, points);
    }

    private async Task<MarkOutcome> SearchForMark(MarkHuntContext hunt)
    {
        if (!await EnterMarkTerritory(hunt))
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.Unreachable;
        }

        var points = ResolveMarkPoints(hunt);
        if (points.Length == 0)
        {
            Warn($"Hunt: none of {hunt.Name}'s spawn points has a floor under it in {hunt.ZoneName}");
            return MarkOutcome.Unreachable;
        }

        hunt.StartClock(hunt.SearchBudgetMs, MarkOutcome.NotFound);
        var laps = hunt.SearchLaps;
        var order = new int[points.Length];
        for (var lap = 1; lap <= laps; lap++)
        {
            OrderMarkPoints(points, order, Svc.Objects.LocalPlayer?.Position ?? points[0]);
            var reachedBefore = hunt.PointsReached;
            for (var orderIndex = 0; orderIndex < order.Length; orderIndex++)
            {
                var scope = $"hunt-lap{lap}-point{orderIndex + 1}/{order.Length}";
                if (await SearchMarkPoint(hunt, points[order[orderIndex]], scope) is { } stop)
                {
                    return stop;
                }
            }

            if (hunt.PointsReached == reachedBefore)
            {
                Warn($"Hunt: could not reach any of {hunt.Name}'s {points.Length} spawn point(s) in {hunt.ZoneName}");
                AnnounceUnreachable(hunt);
                return MarkOutcome.Unreachable;
            }

            var progress = ReadMarkProgress(hunt);
            Diag($"Hunt: lap {lap}/{laps} over {hunt.Name}'s {points.Length} spawn point(s) done, {progress.Killed}/{progress.Needed} credited");
        }

        return MarkOutcome.NotFound;
    }

    private static void AnnounceUnreachable(MarkHuntContext hunt)
    {
        Svc.Chat.PrintError(FlightAccess.IsAvailableIn(hunt.TerritoryId)
            ? $"{AhtConstants.LogPrefix} None of the known spots for {hunt.Name} in {hunt.ZoneName} could be reached; moving on."
            : $"{AhtConstants.LogPrefix} No ground route reaches the known spots for {hunt.Name} in {hunt.ZoneName}, and flying is not unlocked there; moving on.");
    }

    private async Task<MarkOutcome?> SearchMarkPoint(MarkHuntContext hunt, Vector3 point, string scope)
    {
        for (var sighting = 0; sighting <= MaxMarkSightingsPerPoint; sighting++)
        {
            if (await FightVisibleMarks(hunt) is { } fought)
            {
                return fought;
            }

            if (Svc.Condition[ConditionFlag.InCombat])
            {
                await FightOffAttackers(scope);
            }

            if (CheckMarkState(hunt) is { } stop)
            {
                return stop;
            }

            if (await RecoverIfOffMesh(hunt.TerritoryId, point, scope))
            {
                continue;
            }

            var leg = await SweepToMarkPoint(hunt, point, scope);
            if (leg == MarkLeg.Sighted)
            {
                continue;
            }

            if (leg == MarkLeg.Failed)
            {
                return null;
            }

            hunt.PointsReached++;
            await DwellAtMarkPoint(hunt);
            return await FightVisibleMarks(hunt);
        }

        return null;
    }

    // The dwell ends as soon as the mark comes into view.
    private async Task DwellAtMarkPoint(MarkHuntContext hunt)
    {
        Status = hunt.SearchLabel;
        var deadline = Environment.TickCount64 + MarkPointSettleMs;
        while (Environment.TickCount64 < deadline && !CancelToken.IsCancellationRequested)
        {
            if (SightMark(hunt, out _))
            {
                return;
            }

            await DelayMs(MarkScanIntervalMs);
        }
    }

    // Only the direct leg can stop at a sighting. Full travel cannot, so it takes over only once the leg stalls, for its
    // recovery ladder that always makes progress.
    private async Task<MarkLeg> SweepToMarkPoint(MarkHuntContext hunt, Vector3 point, string scope)
    {
        MarkPhase = HuntPhase.Searching;
        Status = hunt.SearchLabel;
        if (WithinReach(point, MarkSweepArriveMeters))
        {
            return MarkLeg.Arrived;
        }

        var sighted = false;
        var nextScanAt = 0L;
        var goal = point;
        var goalTolerance = MarkSweepArriveMeters;

        bool AtGoal() => WithinReach(point, MarkSweepArriveMeters) || WithinReach(goal, goalTolerance);

        bool StopCondition()
        {
            Status = hunt.SearchLabel;
            if (AtGoal())
            {
                return true;
            }

            var now = Environment.TickCount64;
            if (now < nextScanAt)
            {
                return false;
            }

            nextScanAt = now + MarkScanIntervalMs;
            sighted = SightMark(hunt, out _);
            return sighted;
        }

        var mountingAllowed = TerritoryAllowsMount(hunt.TerritoryId);
        var falseStarts = 0;
        var groundLegFailed = false;
        while (true)
        {
            var plan = await PlanLeg(hunt.TerritoryId, point, MarkSweepArriveMeters, MountMinMeters, mountingAllowed, groundStalled: false, scope);
            if (CancelToken.IsCancellationRequested)
            {
                return MarkLeg.Failed;
            }

            if (!plan.Reachable)
            {
                Diag($"{scope}: the nearest floor the ground reaches is {plan.ShortfallMeters:F0}m short of the point; full travel looks for a landmass that reaches it");
                return await TravelTo(hunt.TerritoryId, point, MarkSweepArriveMeters) ? MarkLeg.Arrived : MarkLeg.Failed;
            }

            goal = plan.Target;
            goalTolerance = plan.ToleranceFor(MarkSweepArriveMeters);
            if (AtGoal())
            {
                return MarkLeg.Arrived;
            }

            var legGoal = goal;
            var legMovement = MovementFor(plan.Mode, goalTolerance);
            Diag($"{scope}: going {plan.Mode} {DistanceTo(legGoal):F0}m to {FormatPosition(legGoal)}");
            var startedAt = Environment.TickCount64;
            var operation = new MoveOp(move => move.MoveInZone(legGoal, legMovement, StopCondition));
            var completed = await RunCancellable(operation, TravelBudgetMs(legGoal), scope, StuckDetector.MoveStallAbort(scope));
            if (sighted)
            {
                Diag($"{scope}: {hunt.Name} in view; stopping to fight it");
                return MarkLeg.Sighted;
            }

            if (AtGoal())
            {
                return MarkLeg.Arrived;
            }

            if (CancelToken.IsCancellationRequested)
            {
                return MarkLeg.Failed;
            }

            if (operation.Fault is { } fault)
            {
                Diag($"{scope}: the direct leg faulted: {fault.Message}");
            }

            var falseStart = completed && operation.Fault is null && Environment.TickCount64 - startedAt < StuckDetector.FalseStartMs;
            if (!falseStart || falseStarts >= MaxFalseStarts)
            {
                groundLegFailed = plan.Mode != LegMode.Flight;
                break;
            }

            falseStarts++;
            await RecoverFromFalseStart(scope, falseStarts);
        }

        Diag($"{scope}: the direct leg ended {DistanceTo(point):F0}m short; using full travel");
        return await TravelTo(hunt.TerritoryId, point, MarkSweepArriveMeters, groundLegFailed) ? MarkLeg.Arrived : MarkLeg.Failed;
    }

    private async Task<bool> EnterMarkTerritory(MarkHuntContext hunt)
    {
        if (!await WaitForPlayerReady())
        {
            return false;
        }

        if (Svc.ClientState.TerritoryType != hunt.TerritoryId)
        {
            MarkPhase = HuntPhase.Travelling;
            var entry = hunt.SpawnPoints.Length > 0 ? hunt.SpawnPoints[0] : Vector3.Zero;
            Diag($"Hunt: teleporting to {hunt.ZoneName} ({hunt.TerritoryId}) for {hunt.Name}");
            var reached = false;
            await RunWithStatusPinned(
                $"Teleporting to {hunt.ZoneName}",
                async () => reached = await TeleportToTerritory(hunt.TerritoryId, entry, "hunt-teleport", TravelTeleportWatchdogMs));
            if (!reached)
            {
                if (!CancelToken.IsCancellationRequested)
                {
                    Warn($"Hunt: could not reach {hunt.ZoneName} (still in territory {Svc.ClientState.TerritoryType})");
                }

                return false;
            }
        }

        await WaitForNavmeshReady(TravelNavmeshWaitMs, TravelNavmeshPollFrames);
        return !CancelToken.IsCancellationRequested;
    }

    // A flag and the zone's spawn points carry no height, and a reported height is only a hint, so every point is snapped
    // to the floor once the zone's mesh is loaded.
    private Vector3[] ResolveMarkPoints(MarkHuntContext hunt)
    {
        if (hunt.ResolvedPoints is { } cached)
        {
            return cached;
        }

        var raw = hunt.SpawnPoints;
        var resolved = new List<Vector3>(raw.Length);
        for (var pointIndex = 0; pointIndex < raw.Length; pointIndex++)
        {
            if (SnapMarkHeight(raw[pointIndex]) is { } floor)
            {
                resolved.Add(floor);
            }
        }

        if (resolved.Count < raw.Length)
        {
            Diag($"Hunt: {raw.Length - resolved.Count} of {hunt.Name}'s {raw.Length} spawn point(s) dropped with no floor under them");
        }

        hunt.ResolvedPoints = [.. resolved];
        return hunt.ResolvedPoints;
    }

    // A height hint picks the floor nearest it, so a spawn under a bridge or below a ledge is not lifted to the top layer.
    private protected static Vector3? SnapMarkHeight(Vector3 point)
    {
        if (float.IsNaN(point.Y))
        {
            return SnapUnknownHeight(point);
        }

        var navmesh = NavmeshIPC.Instance;
        var lifted = point with { Y = point.Y + MarkHeightHintLiftMeters };
        return navmesh.PointOnFloor(lifted, allowUnlandable: false, MarkHeightSearchHalfExtentMeters)
            ?? navmesh.PointOnFloor(lifted, allowUnlandable: true, MarkHeightSearchHalfExtentMeters)
            ?? SnapUnknownHeight(point);
    }

    private static Vector3? SnapUnknownHeight(Vector3 point)
    {
        var navmesh = NavmeshIPC.Instance;
        var floor = navmesh.HighestFloor(point, allowUnlandable: false, MarkHeightSearchHalfExtentMeters)
            ?? navmesh.HighestFloor(point, allowUnlandable: true, MarkHeightSearchHalfExtentMeters);
        if (floor is not null)
        {
            return floor;
        }

        var height = Svc.Objects.LocalPlayer?.Position.Y ?? 0f;
        return navmesh.NearestStandablePoint(point with { Y = height }, MarkHeightFallbackHalfExtentMeters, MarkHeightFallbackVerticalMeters);
    }

    private static void OrderMarkPoints(Vector3[] points, int[] order, Vector3 from)
    {
        Span<bool> used = points.Length <= MaxMarkPointsOnStack ? stackalloc bool[points.Length] : new bool[points.Length];
        var position = from;
        for (var slot = 0; slot < points.Length; slot++)
        {
            var nearest = -1;
            var bestDistance = float.PositiveInfinity;
            for (var pointIndex = 0; pointIndex < points.Length; pointIndex++)
            {
                if (used[pointIndex])
                {
                    continue;
                }

                var distance = GroundDistance.SquaredBetween(position, points[pointIndex]);
                if (distance >= bestDistance)
                {
                    continue;
                }

                bestDistance = distance;
                nearest = pointIndex;
            }

            used[nearest] = true;
            order[slot] = nearest;
            position = points[nearest];
        }
    }

    private MarkOutcome? CheckMarkState(MarkHuntContext hunt)
    {
        if (CheckMarkStanding(hunt) is { } stop)
        {
            return stop;
        }

        if (hunt.UncountedKills >= MaxUncountedMarkKills)
        {
            Warn($"Hunt: {hunt.UncountedKills} kills in a row on {hunt.Name} were not credited; giving it up");
            return MarkOutcome.KillsNotCounted;
        }

        return Environment.TickCount64 >= hunt.SearchDeadline ? hunt.ExpiredOutcome : null;
    }

    private MarkOutcome? CheckMarkStanding(MarkHuntContext hunt)
    {
        if (CancelToken.IsCancellationRequested)
        {
            return MarkOutcome.Cancelled;
        }

        if (IsMarkKnockedOut())
        {
            return MarkOutcome.Died;
        }

        return ReadMarkProgress(hunt).Done ? MarkOutcome.Killed : null;
    }

    private static MarkProgress ReadMarkProgress(MarkHuntContext hunt) => new(hunt.CreditedKills, KillsPerMark);

    private bool SightMark(MarkHuntContext hunt, out MarkSighting sighting)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            sighting = default;
            return false;
        }

        var found = MarkFinder.TryFindNearest(hunt.NameId, hunt.HonorsClaims, player.Position, hunt.Ignored, out sighting, out var claimed);
        if (claimed > 0 && !hunt.ClaimSkipLogged)
        {
            hunt.ClaimSkipLogged = true;
            Diag($"Hunt: passing over {claimed} {hunt.Name} another player's party has claimed; kills on them would not count");
        }

        return found;
    }

    private protected static bool IsMarkKnockedOut() => Svc.Condition[ConditionFlag.Unconscious];

    private readonly record struct MarkProgress(int Killed, int Needed)
    {
        public bool Done => Killed >= Needed;
    }

    private sealed class MarkHuntContext
    {
        // Enough to stop re-picking the few copies that could not be reached or finished.
        private const int MaxIgnoredInstances = 8;

        private readonly ulong[] ignored = new ulong[MaxIgnoredInstances];
        private int ignoredCount;
        private int ignoredNext;

        private MarkHuntContext(uint nameId, string name, uint territoryId, Vector3[] spawnPoints, HuntMarkRank? rank, bool fightsSightedOnly)
        {
            NameId = nameId;
            Name = name;
            TerritoryId = territoryId;
            SpawnPoints = spawnPoints;
            MarkRank = rank;
            FightsSightedOnly = fightsSightedOnly;
            AppearsOnTrigger = rank.HasValue && HuntMarkRegistry.AppearsOnTrigger(nameId);
            ZoneName = TerritoryNames.Of(territoryId);
            SearchLabel = $"Searching for {name} in {ZoneName}";
            ApproachLabel = $"Closing in on {name}";
            FightLabel = $"Fighting {name}";
        }

        public static MarkHuntContext ForTrainMark(uint nameId, string name, uint territoryId, Vector3[] spawnPoints)
            => new(nameId, name, territoryId, spawnPoints, RankOf(nameId), fightsSightedOnly: false);

        public static MarkHuntContext ForSightedMark(uint nameId, string name, uint territoryId)
            => new(nameId, name, territoryId, [], RankOf(nameId), fightsSightedOnly: true);

        private static HuntMarkRank? RankOf(uint nameId) => HuntMarkRegistry.TryGet(nameId, out var mark) ? mark.Rank : null;

        public string Name { get; }

        public uint NameId { get; }

        public uint TerritoryId { get; }

        public Vector3[] SpawnPoints { get; }

        public Vector3[]? ResolvedPoints { get; set; }

        public HuntMarkRank? MarkRank { get; }

        public bool IsHuntMark => MarkRank.HasValue;

        // A mark sighted at a flag is fought where it stands; a knockout or a mark gone from view ends it with no search.
        public bool FightsSightedOnly { get; }

        public bool AppearsOnTrigger { get; }

        // A B rank is back soon after it falls, so a sweep that loses one keeps looking; an A or S rank, or a mark that
        // appears on a trigger, stays gone for the rest of the search.
        public bool RespawnsWithinSearch => MarkRank == HuntMarkRank.B && !AppearsOnTrigger;

        // A hunt mark credits everyone who fights it; only an ordinary mob belongs to the party that pulled it.
        public bool HonorsClaims => !IsHuntMark;

        public int SearchBudgetMs => MarkRank is HuntMarkRank.A or HuntMarkRank.S ? RankASearchBudgetMs : RankBSearchBudgetMs;

        public int SearchLaps => AppearsOnTrigger ? TriggeredMarkSearchLaps : MarkSearchLaps;

        // Every hunt mark is a Notorious Monster with far more health than an ordinary mob.
        public int FightBudgetMs => IsHuntMark ? HuntMarkFightBudgetMs : MobFightBudgetMs;

        public int CreditedKills { get; set; }

        public bool ClaimSkipLogged { get; set; }

        public string ZoneName { get; }

        public string SearchLabel { get; }

        public string ApproachLabel { get; }

        public string FightLabel { get; }

        public long SearchDeadline { get; private set; }

        public MarkOutcome ExpiredOutcome { get; private set; } = MarkOutcome.NotFound;

        public int PointsReached { get; set; }

        public int UncountedKills { get; set; }

        public int Knockouts { get; set; }

        public ReadOnlySpan<ulong> Ignored => ignored.AsSpan(0, ignoredCount);

        public void StartClock(int budgetMs, MarkOutcome expiredOutcome)
        {
            SearchDeadline = Environment.TickCount64 + budgetMs;
            ExpiredOutcome = expiredOutcome;
        }

        public void Ignore(ulong gameObjectId)
        {
            ignored[ignoredNext] = gameObjectId;
            ignoredNext = (ignoredNext + 1) % MaxIgnoredInstances;
            ignoredCount = Math.Min(ignoredCount + 1, MaxIgnoredInstances);
        }
    }
}
