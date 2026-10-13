using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Travel;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// Where a train starts, for its details, its chat line and its relay: the zone and aetheryte the relay's ids resolved
// to, else the names its header gave, and the map spot to flag.
internal static class TrainFacts
{
    public static string StartZone(in Announcement announcement)
        => announcement.NamesTerritory
            ? TerritoryNames.Of(announcement.TerritoryId)
            : AnnouncementBody.ReadHeaderField(announcement.Message, AnnouncementBody.StartZoneLabel);

    public static string Aetheryte(in Announcement announcement)
        => announcement.NamesAetheryte && ZoneAetherytes.TryFindById(announcement.AetheryteId, out _, out var aetheryte)
            ? aetheryte.Name
            : AnnouncementBody.ReadHeaderField(announcement.Message, AnnouncementBody.AetheryteLabel);

    // Whole minutes toward zero, so a countdown changes once a minute and "starting now" covers the minute around the start.
    public static int WholeMinutesToStart(in Announcement announcement, DateTime nowUtc)
        => (int)((long)announcement.LeadAt(nowUtc).TotalSeconds / TimeUnits.SecondsPerMinute);

    public static string CountdownText(int minutes)
        => minutes > 0 ? Loc.Plural(L.Feed.InMinutes, minutes)
        : minutes < 0 ? Loc.Plural(L.Feed.StartedAgo, -minutes)
        : Loc.T(L.Feed.StartingNow);

    // Clamped at zero, so a relay clock running ahead reads as just reported rather than as a negative age.
    public static int WholeMinutesSince(DateTime utc, DateTime nowUtc)
        => Math.Max(0, (int)((long)(nowUtc - utc).TotalSeconds / TimeUnits.SecondsPerMinute));

    // A post that gave no start says only when it was written, so it reads as its age rather than as a countdown that
    // would call the train started.
    public static string StartText(in Announcement announcement, DateTime nowUtc)
        => announcement.StartIsGuessed
            ? Loc.Plural(L.Feed.ReportedAgo, WholeMinutesSince(announcement.StartAtUtc, nowUtc))
            : CountdownText(WholeMinutesToStart(announcement, nowUtc));

    // The relay's own coordinates first, else the first pair the post gives; a spot needs a known zone to be flagged in.
    public static bool TryFlagPoint(in Announcement announcement, out Vector2 coordinates)
    {
        coordinates = default;
        if (!announcement.NamesTerritory)
        {
            return false;
        }

        if (announcement.MapCoordinates is { } relayed)
        {
            coordinates = relayed;
            return true;
        }

        return AnnouncementBody.TryReadCoordinates(announcement.Message, out coordinates);
    }
}
