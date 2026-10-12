namespace AutoHuntTrain.Core.Feed;

// Feed rules that belong to one-off S-rank relays rather than scheduled trains. Keeping these rules separate prevents
// train start times, catch-up windows and route matching from being applied to an S rank.
internal static class SRankFeedPolicy
{
    // This controls how long a relay remains visible in Auto Hunt Train. It does not imply that the mark is still alive.
    public static readonly TimeSpan ListedAfterReport = TimeSpan.FromMinutes(10);

    // Repeated relays for the same mark should refresh one feed row rather than create duplicates. The instance is part
    // of the identity because the same territory can host separate copies of the mark at the same time.
    public static bool SameTarget(in Announcement left, in Announcement right)
        => left.IsSRank
            && right.IsSRank
            && left.World.Id == right.World.Id
            && left.TerritoryId == right.TerritoryId
            && left.Instance == right.Instance
            && left.CreatureNameId == right.CreatureNameId;

    public static bool IsExpired(in Announcement announcement, DateTime nowUtc)
        => announcement.IsSRank
            && nowUtc - announcement.PostedAtUtc > ListedAfterReport;

    // Preserve the existing row ID so an open details page and taken-announcement tracking continue to refer to the
    // same feed item. Every other value comes from the newest relay, including corrected coordinates or destination
    // information. Callers should pass the chronologically newer relay as updated.
    public static Announcement Refresh(in Announcement existing, in Announcement updated)
        => updated with
        {
            Id = existing.Id,
            Kind = AnnouncementKind.SRank,
        };
}
