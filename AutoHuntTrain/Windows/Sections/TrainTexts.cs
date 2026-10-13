using AutoHuntTrain.Core;
using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// Texts the feed list and announcement details both show, cached so drawing them every frame allocates nothing. The
// class keeps its original name to avoid a broad UI rename while supporting both scheduled trains and one-off S ranks.
internal static class TrainTexts
{
    private const int FreshReportMinutes = 5;
    private const int ExpiringReportMinutes = 8;

    public static string Status(
        ref CachedText cache,
        in Announcement announcement,
        DateTime nowUtc,
        out Vector4 color)
        => announcement.IsSRank
            ? ReportedAge(ref cache, announcement, nowUtc, out color)
            : announcement.StartIsGuessed
                ? GuessedStartAge(ref cache, announcement, nowUtc, out color)
                : Countdown(ref cache, announcement, nowUtc, out color);

    public static string Countdown(
        ref CachedText cache,
        in Announcement announcement,
        DateTime nowUtc,
        out Vector4 color)
    {
        var minutes = TrainFacts.WholeMinutesToStart(announcement, nowUtc);
        color = minutes < 0
            ? Styling.AccentAmber
            : minutes == 0
                ? Styling.AccentMintSoft
                : Styling.TextSecondary;
        var key = HashCode.Combine(announcement.Id, AnnouncementKind.Train, minutes);
        if (cache.TryGet(key, out var text))
        {
            return text;
        }

        return cache.Set(key, TrainFacts.CountdownText(minutes));
    }

    public static string ReportedAge(
        ref CachedText cache,
        in Announcement announcement,
        DateTime nowUtc,
        out Vector4 color)
    {
        // Future relay clocks are shown as just reported rather than producing a negative age.
        var elapsed = nowUtc - announcement.PostedAtUtc;
        var minutes = Math.Max(
            0,
            (int)((long)elapsed.TotalSeconds / TimeUnits.SecondsPerMinute));
        color = minutes < FreshReportMinutes
            ? Styling.AccentMintSoft
            : minutes < ExpiringReportMinutes
                ? Styling.AccentAmber
                : Styling.TextSecondary;
        var key = HashCode.Combine(announcement.Id, AnnouncementKind.SRank, minutes);
        if (cache.TryGet(key, out var text))
        {
            return text;
        }

        return cache.Set(key, Loc.Plural(L.Feed.ReportedAgo, minutes));
    }

    // A post that gave no start says only when it was written, so the train shows its age rather than a countdown that
    // would call it started; it turns amber once the grace after the post runs out and it counts as in progress.
    private static string GuessedStartAge(
        ref CachedText cache,
        in Announcement announcement,
        DateTime nowUtc,
        out Vector4 color)
    {
        var minutes = Math.Max(
            0,
            (int)((long)(nowUtc - announcement.StartAtUtc).TotalSeconds / TimeUnits.SecondsPerMinute));
        color = announcement.InProgressAt(nowUtc) ? Styling.AccentAmber : Styling.TextSecondary;
        var key = HashCode.Combine(announcement.Id, StartSource.PostTime, minutes);
        if (cache.TryGet(key, out var text))
        {
            return text;
        }

        return cache.Set(key, Loc.Plural(L.Feed.ReportedAgo, minutes));
    }

    // A ride on a train in progress catches up with it, unless there is no route to catch up along or its start was
    // only guessed. One-off S ranks never catch up along a route, even though their compatibility StartAtUtc contains
    // the report time.
    public static bool CatchesUp(in Announcement announcement, DateTime nowUtc)
        => announcement.CatchUpDueAt(nowUtc)
            && TrainRoutes.TryFor(announcement, out _);

    public static string RideHint(in Announcement announcement, bool catchesUp)
        => Loc.T(
            announcement.IsSRank
                ? L.Feed.SHuntHint
                : catchesUp
                    ? L.Feed.RideCatchUpHint
                    : L.Feed.RideHint);

    // A manual action button is only ever refused for what makes its hunt impossible right now.
    public static string Verdict(RideVerdict verdict) => Loc.T(verdict switch
    {
        RideVerdict.OutOfRegion => L.Feed.VerdictOutOfRegion,
        RideVerdict.InDuty => L.Feed.VerdictInDuty,
        RideVerdict.LifestreamBusy => L.Feed.VerdictLifestreamBusy,
        RideVerdict.RideRunning => L.Feed.VerdictRideRunning,
        RideVerdict.SRankCrossDataCenter => L.Feed.VerdictSRankCrossDataCenter,
        _ => L.Feed.VerdictFeedWorldUnknown,
    });
}
