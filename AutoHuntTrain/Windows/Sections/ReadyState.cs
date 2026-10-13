using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

internal static class ReadyState
{
    public enum Kind { SetupNeeded, FeedReady, Running, Paused }

    public readonly record struct Info(Kind Kind, Vector4 Accent, Vector4 AccentSoft, FontAwesomeIcon Icon, string Title, string Detail);

    private static readonly CachedText[] details = new CachedText[6];

    private static CachedText catchUpLine;

    private static int cachedFrame = -1;
    private static Info cached;

    public static Info Resolve(AutoHuntController controller)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cached;
        }

        cached = Compute(controller);
        cachedFrame = frame;
        return cached;
    }

    private static Info Compute(AutoHuntController controller)
    {
        if (controller.Running)
        {
            if (controller.Paused)
            {
                var detail = controller.PauseReason == PauseReason.InContent
                    ? Loc.T(L.Train.DetailPausedInContent)
                    : Loc.T(L.Train.DetailPausedManual);
                return new Info(Kind.Paused, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Pause, Loc.T(L.Train.TitlePaused), detail);
            }

            var sRank = controller.SessionSnapshot?.IsSRank == true;
            return new Info(
                Kind.Running,
                Styling.AccentBlue,
                Styling.AccentBlueSoft,
                sRank ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Train,
                sRank ? Loc.T(L.Feed.SHunt) : Loc.T(L.Train.TitleRiding),
                ActivityLabel(controller));
        }

        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return new Info(Kind.SetupNeeded, Styling.AccentRose, Styling.AccentRoseSoft, FontAwesomeIcon.ExclamationTriangle,
                Loc.T(L.Train.TitleSetupNeeded), Loc.T(L.Train.DetailSetupNeeded));
        }

        // HuntAlerts is required, so past the setup check the feed is loaded and a ride needs no conductor of the player's own.
        return new Info(Kind.FeedReady, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.Rss,
            Loc.T(L.Train.TitleFeedReady), FeedDetail(Kind.FeedReady, L.Feed.HeadlineHuntAlerts, L.Train.DetailFeedReady));
    }

    // The detail line opens with which feed the trains come from; composed once per kind and language.
    private static string FeedDetail(Kind kind, LocString feedLine, LocString detail)
    {
        ref var cache = ref details[(int)kind];
        if (cache.TryGet((int)kind, out var text))
        {
            return text;
        }

        return cache.Set((int)kind, Loc.T(L.Feed.HeadlineDetail, Loc.T(feedLine), Loc.T(detail)));
    }

    public static string ShortLabel(Kind kind) => kind switch
    {
        Kind.Running       => Loc.T(L.Shell.StatusRunning),
        Kind.Paused        => Loc.T(L.Shell.StatusPaused),
        Kind.FeedReady     => Loc.T(L.Shell.StatusFeedReady),
        Kind.SetupNeeded   => Loc.T(L.Shell.StatusSetupNeeded),
        _                  => Loc.T(L.Shell.StatusIdle),
    };

    // The ride phase names what the ride is doing between marks; while a mark is engaged the hunt's own phase is the
    // finer word, fighting or travelling back after a knockout. Catching up names the zone and how far along the route it is.
    public static string ActivityLabel(AutoHuntController controller)
    {
        var phase = controller.Phase;
        var progress = controller.Progress;
        var ridePhase = progress.RidePhase;
        if (ridePhase is RidePhase.None or RidePhase.Engaging || phase is not (HuntPhase.Waiting or HuntPhase.Travelling))
        {
            return PhaseLabel(phase);
        }

        return ridePhase == RidePhase.CatchingUp ? CatchUpLine(progress) : RidePhaseLabel(ridePhase);
    }

    // The label already says where a catch-up is, so the detail says who the ride is waiting to hear from.
    public static string ActivityDetail(AutoHuntController controller)
    {
        var sRank = controller.SessionSnapshot?.IsSRank == true;
        if (!sRank && controller.Progress.CatchingUp)
        {
            return ConductorLine.Get(controller.Progress);
        }

        if (CurrentMark.TryGet(controller, out var mark))
        {
            return mark.Line;
        }

        return !sRank && CurrentFlag.TryGet(controller, out var flag)
            ? flag.Line
            : controller.Status;
    }

    public static string PhaseLabel(HuntPhase phase) => phase switch
    {
        HuntPhase.Preparing  => Loc.T(L.Run.PhasePreparing),
        HuntPhase.Travelling => Loc.T(L.Run.PhaseTravelling),
        HuntPhase.Searching  => Loc.T(L.Run.PhaseSearching),
        HuntPhase.Fighting   => Loc.T(L.Run.PhaseFighting),
        HuntPhase.Upkeep     => Loc.T(L.Progress.PhaseUpkeep),
        HuntPhase.Finishing  => Loc.T(L.Run.PhaseFinishing),
        _                    => Loc.T(L.Run.PhaseStandingBy),
    };

    public static string RidePhaseLabel(RidePhase phase) => phase switch
    {
        RidePhase.Journey        => Loc.T(L.Feed.PhaseJourney),
        RidePhase.CatchingUp     => Loc.T(L.Ride.PhaseCatchingUp),
        RidePhase.Travelling     => Loc.T(L.Ride.PhaseTravelling),
        RidePhase.AtFlag         => Loc.T(L.Ride.PhaseAtFlag),
        RidePhase.WaitingForMark => Loc.T(L.Ride.PhaseWaitingForMark),
        RidePhase.Engaging       => Loc.T(L.Run.PhaseFighting),
        _                        => Loc.T(L.Ride.PhaseWaiting),
    };

    public static string CatchUpLine(RideProgress progress)
    {
        var territoryId = progress.CatchUpTerritoryId;
        if (territoryId == 0)
        {
            return RidePhaseLabel(RidePhase.CatchingUp);
        }

        var key = ((long)territoryId << 32) | ((long)progress.CatchUpStop << 17) | ((long)progress.CatchUpStops << 1) | (progress.CatchUpListening ? 1L : 0L);
        if (catchUpLine.TryGet(key, out var line))
        {
            return line;
        }

        var text = progress.CatchUpListening ? L.Ride.CatchUpListening : L.Ride.CatchUpTeleporting;
        return catchUpLine.Set(key, Loc.T(text, TerritoryNames.Of(territoryId), NumberText.Of(progress.CatchUpStop), NumberText.Of(progress.CatchUpStops)));
    }
}
