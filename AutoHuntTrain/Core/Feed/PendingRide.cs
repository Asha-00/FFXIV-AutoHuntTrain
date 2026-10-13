using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// An announced ride on its way to another data center, written before the transfer that logs the character out, so a
// login can rebuild the ride when its task did not live through the relog. The announcement's own text is left out:
// the ride never reads it. A ride saved before the start source was kept comes back with its start taken as given.
public readonly record struct PendingRide(
    uint WorldId,
    string WorldName,
    ExpansionGroup Group,
    DateTime StartAtUtc,
    StartSource StartSource,
    DateTime EarliestStartAtUtc,
    uint AetheryteId,
    uint TerritoryId,
    int Instance,
    float? MapX,
    float? MapY,
    string ConductorName,
    uint ConductorWorldId,
    DateTime SessionStartedAtUtc,
    int MarksCredited,
    bool ArrivedAtTrain,
    DateTime SavedAtUtc)
{
    internal static PendingRide From(in Announcement announcement, AutoHuntSession session)
    {
        var coordinates = announcement.MapCoordinates;
        return new PendingRide(
            announcement.World.Id,
            announcement.World.Name,
            announcement.Group,
            announcement.StartAtUtc,
            announcement.StartSource,
            announcement.EarliestStartAtUtc,
            announcement.AetheryteId,
            announcement.TerritoryId,
            announcement.Instance,
            coordinates?.X,
            coordinates?.Y,
            announcement.Conductor.Name,
            announcement.Conductor.WorldId,
            session.StartedAt,
            session.MarksCredited,
            session.ArrivedAtTrain,
            DateTime.UtcNow);
    }

    // Id 0 is never handed out by the feed, so the rebuilt train cannot shadow a listed one.
    internal bool TryToAnnouncement(out Announcement announcement)
    {
        if (!Worlds.TryFindById(WorldId, out var world))
        {
            announcement = default;
            return false;
        }

        Vector2? coordinates = MapX is { } x && MapY is { } y ? new Vector2(x, y) : null;
        var conductor = new ConductorIdentity(ConductorName ?? string.Empty, ConductorWorldId);
        announcement = new Announcement(0, SavedAtUtc, SavedAtUtc, world, Group, StartAtUtc, AetheryteId, TerritoryId, Instance, coordinates, conductor, string.Empty)
        {
            StartSource = StartSource,
            EarliestStartAtUtc = EarliestStartAtUtc,
        };
        return true;
    }
}
