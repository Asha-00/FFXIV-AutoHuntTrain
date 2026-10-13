using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// A train already in progress has left its start zone, and Shout reaches only the zone it is posted in. So the ride
// guesses from the time since the start how far along its usual route the train has got, teleports there and listens
// for a flag; nothing heard moves it one zone on, until the route runs out or the train would have left the list.
internal sealed partial class AutoRide
{
    private const int CatchUpListenMs = 60_000;
    private const string CatchUpLabel = "catch-up";

    private bool catchingUp;
    private bool catchUpListening;
    private DateTime catchUpListenSinceUtc;
    private TrainRoute catchUpRoute;

    private async Task<bool> CatchUp(Announcement plan, TrainRoute route)
    {
        catchingUp = true;
        catchUpRoute = route;
        try
        {
            return await FollowRouteUntilHeard(plan, route);
        }
        finally
        {
            catchingUp = false;
            catchUpListening = false;
            progress.SetCatchUp(0, 0, 0, listening: false);
        }
    }

    private async Task<bool> FollowRouteUntilHeard(Announcement plan, TrainRoute route)
    {
        var elapsed = DateTime.UtcNow - plan.StartAtUtc;
        var ahead = FirstCatchUpStep(elapsed, route);
        progress.SetRidePhase(RidePhase.CatchingUp);
        Diag(ahead == 0
            ? $"Ride: the {ExpansionGroups.Name(plan.Group)} train on {plan.World.Name} started {elapsed.TotalMinutes:F1} min ago, within a zone's {route.MinutesPerZone:F1} min of counting as in progress, so it may not have left {StartZoneName()} yet; listening there first along the {route.Expansion.ShortName()} route {RouteText(route)}"
            : $"Ride: the {ExpansionGroups.Name(plan.Group)} train on {plan.World.Name} started {elapsed.TotalMinutes:F1} min ago and has left {StartZoneName()}; at {route.MinutesPerZone:F1} min a zone it should be {ahead} zone(s) along the {route.Expansion.ShortName()} route {RouteText(route)}");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} The train started {elapsed.TotalMinutes:F0} minutes ago; catching up with it along its usual route.");
        for (var step = ahead; step < route.Length; step++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return false;
            }

            if (conductorEnded)
            {
                EndOnConductorPhrase();
                return false;
            }

            if (PastListing(plan))
            {
                EndCatchUp($"the train is more than {Announcement.ListedAfterStart.TotalMinutes:F0} minutes past its start");
                return false;
            }

            var territoryId = route.ZoneAt(step);
            if (!TryCatchUpDestination(territoryId, out var destination))
            {
                Diag($"{CatchUpLabel}: no attuned aetheryte leads into {TerritoryNames.Of(territoryId)}; stepping on to the next zone");
                continue;
            }

            if (!await TeleportForCatchUp(territoryId, destination, step, route.Length))
            {
                continue;
            }

            if (await ListenForTrain(plan, territoryId, step, route.Length))
            {
                session.ArrivedAtTrain = true;
                lastFlagAtMs = Environment.TickCount64;
                Diag($"Ride: caught up with the train in {TerritoryNames.Of(territoryId)}, following {Conductor.Describe(conductor)} ({ConditionTag()})");
                return true;
            }
        }

        if (conductorEnded)
        {
            EndOnConductorPhrase();
        }
        else if (!CancelToken.IsCancellationRequested)
        {
            EndCatchUp($"no flag was heard in any zone up to the end of the {route.Expansion.ShortName()} route");
        }

        return false;
    }

    // Conductors often set off a little late, so a train only just in progress is looked for in its start zone first,
    // until one zone's time past the point it counts as in progress.
    private static int FirstCatchUpStep(TimeSpan elapsed, in TrainRoute route)
    {
        var minutes = elapsed.TotalMinutes;
        if (minutes <= Announcement.InProgressAfter.TotalMinutes + route.MinutesPerZone)
        {
            return 0;
        }

        return Math.Min((int)Math.Floor(minutes / route.MinutesPerZone), route.Length - 1);
    }

    // A zone owning no aetheryte is entered through its gateway, which has to be attuned instead. Elsewhere the zone's
    // centre is aimed at, so the teleport lands at the attuned aetheryte best placed for wherever the next flag goes up,
    // with the known traps counted.
    private static bool TryCatchUpDestination(uint territoryId, out Vector3 destination)
    {
        destination = Vector3.Zero;
        if (Svc.ClientState.TerritoryType == territoryId)
        {
            return true;
        }

        if (ZoneAetherytes.TryFindGateway(territoryId, out var gateway))
        {
            return ZoneAetherytes.IsAttuned(gateway.AetheryteId);
        }

        return ZoneAetherytes.TryFindNearest(territoryId, destination, out _);
    }

    private async Task<bool> TeleportForCatchUp(uint territoryId, Vector3 destination, int step, int stops)
    {
        var zoneName = TerritoryNames.Of(territoryId);
        progress.SetCatchUp(territoryId, step + 1, stops, listening: false);
        var reached = false;
        await RunWithStatusPinned(
            $"Catching up: teleporting to {zoneName} ({step + 1} of {stops})",
            async () =>
            {
                var label = $"{CatchUpLabel}-{step + 1}";
                if (await HumanDelayBeforeTeleport(territoryId, label))
                {
                    reached = await TeleportToTerritory(territoryId, destination, label, ZoneTeleportWatchdogMs);
                }
            });
        if (!reached && !CancelToken.IsCancellationRequested)
        {
            Warn($"{CatchUpLabel}: could not reach {zoneName} (still in territory {Svc.ClientState.TerritoryType}); stepping on to the next zone");
        }

        return reached;
    }

    // A flag from the conductor already known, or the first flag anyone posts once the character is in the zone, ends
    // the wait; OnPosted sets it pending either way.
    private async Task<bool> ListenForTrain(Announcement plan, uint territoryId, int step, int stops)
    {
        var zoneName = TerritoryNames.Of(territoryId);
        NoteInstanceLimit(zoneName);
        catchUpListenSinceUtc = DateTime.UtcNow;
        catchUpListening = true;
        progress.SetCatchUp(territoryId, step + 1, stops, listening: true);
        Status = $"Catching up: listening in {zoneName} ({step + 1} of {stops})";
        Diag($"{CatchUpLabel}: listening in {zoneName} ({step + 1} of {stops}) for up to {CatchUpListenMs / TimeUnits.MillisecondsPerSecond}s ({ConditionTag()})");
        var deadline = Environment.TickCount64 + CatchUpListenMs;
        try
        {
            while (!CancelToken.IsCancellationRequested && !conductorEnded)
            {
                if (hasPending)
                {
                    return true;
                }

                if (Environment.TickCount64 >= deadline || PastListing(plan))
                {
                    Diag($"{CatchUpLabel}: no flag heard in {zoneName}");
                    return false;
                }

                await NextFrame(WaitPollFrames);
            }

            return false;
        }
        finally
        {
            catchUpListening = false;
        }
    }

    // Instances are not switched while catching up: the train could be in any of them and a minute is too short to try each.
    private void NoteInstanceLimit(string zoneName)
    {
        var lifestream = LifestreamIPC.Instance;
        var count = lifestream.NumberOfInstances();
        if (count == 0)
        {
            return;
        }

        Diag($"{CatchUpLabel}: {zoneName} has {count} instances; listening only in instance {lifestream.CurrentInstance()}, where the teleport landed, so a train in another one is not heard");
    }

    private bool QualifiesWhileCatchingUp(in FlagPost post)
        => catchUpListening
        && post.PostedAtUtc >= catchUpListenSinceUtc
        && catchUpRoute.Contains(post.TerritoryId);

    private static bool PastListing(in Announcement plan)
        => DateTime.UtcNow - plan.StartAtUtc > Announcement.ListedAfterStart;

    private void EndCatchUp(string reason)
    {
        session.Outcome = RideOutcome.Abandoned;
        session.CompletedByStopCondition = true;
        Status = "Ride ended";
        Warn($"Ride: could not find the train while catching up: {reason}; the ride ends");
        Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Could not find the train; it may have taken another route or finished.");
    }

    private static string RouteText(in TrainRoute route)
    {
        var names = new string[route.Length];
        for (var step = 0; step < route.Length; step++)
        {
            names[step] = TerritoryNames.Of(route.ZoneAt(step));
        }

        return string.Join(", ", names);
    }
}
