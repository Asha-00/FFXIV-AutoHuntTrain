using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;

namespace AutoHuntTrain.Core.Tasks;

public abstract partial class AutoCommon
{
    // Never route toward an untouched S rank's centre. After locating the exact live actor, stage on navigable terrain
    // forty metres away on the player's side of it, then wait for another player to pull.
    private const float SRankStagingDistanceMeters = 30f;
    private const float SRankStagingArriveMeters = 4f;
    private const float SRankStagingFloorHalfExtentMeters = 12f;
    private const float SRankStagingFloorLiftMeters = 8f;
    private const float SRankDirectionEpsilon = 1f;
    private const int SRankSearchTimeoutMs = 5 * TimeUnits.MillisecondsPerMinute;
    private const int SRankPullTimeoutMs = 10 * TimeUnits.MillisecondsPerMinute;
    private const int SRankScanPollMs = 250;

    // Searches only HuntAlerts' reported point for the exact announced BNpcName. The generic train hunt is deliberately
    // not used: it would merge every known spawn point and may immediately engage when the user disabled WaitForPull.
    private protected async Task<MarkOutcome> HuntReportedSRank(
        uint nameId,
        uint territoryId,
        Vector3 reportedPoint)
    {
        if (CancelToken.IsCancellationRequested)
        {
            return MarkOutcome.Cancelled;
        }

        if (Svc.ClientState.TerritoryType != territoryId)
        {
            Warn($"S rank: expected territory {territoryId}, but the character is in {Svc.ClientState.TerritoryType}");
            return MarkOutcome.Unreachable;
        }

        MarkPhase = HuntPhase.Searching;
        var name = HuntMarkRegistry.NameOf(nameId);
        Status = $"Searching for {name}";

        // S ranks are visible throughout their zone. Use the exact live actor to obtain a trustworthy elevation and
        // construct a destination beside it; the rounded relay point is retained only as context and a direction fallback.
        var sighting = await WaitForExactSRank(nameId, name, SRankSearchTimeoutMs);
        if (sighting is null)
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.NotFound;
        }

        var gameObjectId = sighting.Value.GameObjectId;
        var discrepancy = HorizontalDistance(reportedPoint, sighting.Value.Position);
        Diag($"S rank: {name}'s live position is {discrepancy:F0}m from the reported point; using the live actor to choose a safe staging position");
        if (!await StageNearSRank(nameId, name, territoryId, gameObjectId, sighting.Value, reportedPoint))
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.Unreachable;
        }

        if (!TryRefreshExactSRank(nameId, gameObjectId, out var staged))
        {
            Diag($"S rank: {name} disappeared while approaching the safe staging point");
            return MarkOutcome.NotFound;
        }

        var pulled = await WaitForSRankPull(nameId, name, sighting.Value);
        if (pulled is null)
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.NotFound;
        }

        Diag($"S rank: {name} was pulled; joining the fight");
        return await HuntSightedMark(nameId, territoryId, pulled.Value);
    }

    // A coordinate-free relay can still be followed because S ranks are visible in the object table throughout their
    // zone. The live actor position is used only as a staging destination; exact NameId/GameObjectId tracking and the
    // mandatory external-pull gate remain unchanged.
    private protected async Task<MarkOutcome> HuntVisibleSRank(uint nameId, uint territoryId)
    {
        if (CancelToken.IsCancellationRequested)
        {
            return MarkOutcome.Cancelled;
        }

        if (Svc.ClientState.TerritoryType != territoryId)
        {
            Warn($"S rank: expected territory {territoryId}, but the character is in {Svc.ClientState.TerritoryType}");
            return MarkOutcome.Unreachable;
        }

        MarkPhase = HuntPhase.Searching;
        var name = HuntMarkRegistry.NameOf(nameId);
        Status = $"Locating {name} in {TerritoryNames.Of(territoryId)}";
        var sighting = await WaitForExactSRank(nameId, name, SRankSearchTimeoutMs, "in the announced zone");
        if (sighting is null)
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.NotFound;
        }

        var gameObjectId = sighting.Value.GameObjectId;
        if (!await StageNearSRank(nameId, name, territoryId, gameObjectId, sighting.Value, fallbackPoint: null))
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.Unreachable;
        }

        if (!TryRefreshExactSRank(nameId, gameObjectId, out var staged))
        {
            Diag($"S rank: {name} disappeared while approaching the safe staging point");
            return MarkOutcome.NotFound;
        }

        var pulled = await WaitForSRankPull(nameId, name, staged);
        if (pulled is null)
        {
            return CancelToken.IsCancellationRequested ? MarkOutcome.Cancelled : MarkOutcome.NotFound;
        }

        Diag($"S rank: {name} was pulled; joining the fight");
        return await HuntSightedMark(nameId, territoryId, pulled.Value);
    }

    private async Task<bool> StageNearSRank(
        uint nameId,
        string name,
        uint territoryId,
        ulong gameObjectId,
        MarkSighting initial,
        Vector3? fallbackPoint)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var target = initial.Position;
        var away = new Vector2(player.Position.X - target.X, player.Position.Z - target.Z);
        if (away.LengthSquared() < SRankDirectionEpsilon * SRankDirectionEpsilon && fallbackPoint is { } fallback)
        {
            away = new Vector2(fallback.X - target.X, fallback.Z - target.Z);
        }

        if (away.LengthSquared() < SRankDirectionEpsilon * SRankDirectionEpsilon)
        {
            away = Vector2.UnitX;
        }
        else
        {
            away = Vector2.Normalize(away);
        }

        var raw = new Vector3(
            target.X + away.X * SRankStagingDistanceMeters,
            target.Y,
            target.Z + away.Y * SRankStagingDistanceMeters);
        var navmesh = NavmeshIPC.Instance;
        var lifted = raw with { Y = raw.Y + SRankStagingFloorLiftMeters };
        var staging = navmesh.PointOnFloor(
                lifted,
                allowUnlandable: false,
                SRankStagingFloorHalfExtentMeters)
            ?? navmesh.PointOnFloor(
                lifted,
                allowUnlandable: true,
                SRankStagingFloorHalfExtentMeters);

        // The remote tile may not be built yet. The actor's live height is still a safe hint compared with the relay's
        // synthetic Y=0; TravelTo builds the route and performs its normal destination snap once navigation starts.
        var destination = staging ?? raw;
        var actorDistance = HorizontalDistance(destination, target);
        if (actorDistance < SRankStagingDistanceMeters - SRankStagingFloorHalfExtentMeters)
        {
            Warn($"S rank: the nearest staging floor is only {actorDistance:F0}m from {name}; the untouched mark will not be approached");
            return false;
        }

        Diag(staging is { }
            ? $"S rank: selected a safe staging point {actorDistance:F0}m from {name} on terrain at {FormatPosition(destination)}"
            : $"S rank: the remote staging floor is not built yet; using {name}'s live height at {FormatPosition(destination)} and letting travel resolve the terrain");
        Status = $"Moving to a safe position near {name}";
        if (!await TravelTo(territoryId, destination, SRankStagingArriveMeters))
        {
            return false;
        }

        if (Svc.Condition[ConditionFlag.Mounted])
        {
            Status = $"Dismounting near {name}";
            Diag($"S rank: safely dismounting at the staging point before waiting for {name}");
            await SafeDismount("s-rank-stage-dismount");

            if (CancelToken.IsCancellationRequested)
            {
                return false;
            }
        }

        if (!TryRefreshExactSRank(nameId, gameObjectId, out var refreshed))
        {
            return false;
        }

        var finalDistance = HorizontalDistance(player.Position, refreshed.Position);
        Diag($"S rank: staged {finalDistance:F0}m from {name}; waiting clear of the untouched mark");
        return true;
    }

    private static float HorizontalDistance(in Vector3 left, in Vector3 right)
    {
        var x = left.X - right.X;
        var z = left.Z - right.Z;
        return MathF.Sqrt(x * x + z * z);
    }

    private static bool TryFindExactSRank(uint nameId, out MarkSighting sighting)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            sighting = default;
            return false;
        }

        return MarkFinder.TryFindNearest(
            nameId,
            honorClaims: false,
            player.Position,
            ReadOnlySpan<ulong>.Empty,
            out sighting,
            out _);
    }

    private async Task<MarkSighting?> WaitForExactSRank(uint nameId, string name, int timeoutMs, string searchContext = "at the reported location")
    {
        MarkPhase = HuntPhase.Searching;
        Status = $"Searching for {name} {searchContext}";
        var deadline = Environment.TickCount64 + timeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return null;
            }

            if (TryFindExactSRank(nameId, out var sighting))
            {
                Diag($"S rank: {name} sighted {sighting.DistanceToHitbox:F0}m away at {FormatPosition(sighting.Position)}");
                return sighting;
            }

            await DelayMs(SRankScanPollMs);
        }

        Diag($"S rank: {name} was not found {searchContext} within {timeoutMs / TimeUnits.MillisecondsPerMinute} minutes");
        return null;
    }

    // S ranks always wait for an external pull, regardless of the train WaitForPull setting. Combat remains inactive
    // until the live target has lost HP or the game marks it in combat.
    private async Task<MarkSighting?> WaitForSRankPull(uint nameId, string name, MarkSighting initial)
    {
        MarkPhase = HuntPhase.Waiting;
        Status = $"Waiting for someone to pull {name}";
        Diag($"S rank: {name} sighted; waiting for another player to pull");

        var gameObjectId = initial.GameObjectId;
        var current = initial;
        var deadline = Environment.TickCount64 + SRankPullTimeoutMs;
        while (Environment.TickCount64 < deadline)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return null;
            }

            if (!TryRefreshExactSRank(nameId, gameObjectId, out current))
            {
                Diag($"S rank: {name} disappeared before the pull");
                return null;
            }

            if (IsPulledSRank(current))
            {
                return current;
            }

            await DelayMs(SRankScanPollMs);
        }

        Diag($"S rank: nobody pulled {name} within {SRankPullTimeoutMs / TimeUnits.MillisecondsPerMinute} minutes");
        return null;
    }

    private static bool TryRefreshExactSRank(uint nameId, ulong gameObjectId, out MarkSighting sighting)
    {
        sighting = default;
        if (Svc.Objects.LocalPlayer is not { } player
            || !MarkFinder.TryGetLive(gameObjectId, player.Position, out var live)
            || MarkFinder.Resolve(live) is not IBattleNpc npc
            || npc.NameId != nameId
            || !npc.IsTargetable
            || npc.CurrentHp == 0)
        {
            return false;
        }

        sighting = live;
        return true;
    }

    private static bool IsPulledSRank(in MarkSighting sighting)
        => MarkFinder.Resolve(sighting) is IBattleNpc npc
            && (npc.CurrentHp < npc.MaxHp
                || (npc.StatusFlags & StatusFlags.InCombat) != 0);
}
