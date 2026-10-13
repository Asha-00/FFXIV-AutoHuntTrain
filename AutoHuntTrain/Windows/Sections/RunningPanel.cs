using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

internal static class RunningPanel
{
    private const float PadX = 18f;
    // Sized to sit on a line of body text.
    private const float InlineRankSize = 18f;
    private const float RankGap = 8f;
    private const float ConductorLineGap = 4f;
    private const float CardPadY = 16f;
    private const float RingInset = 18f;
    private const float RingGap = 20f;
    private const float ChipPadX = 9f;
    private const float ChipPadY = 3f;
    private const float ChipGap = 10f;
    private const float SubLineGap = 3f;
    private const float BodyGap = 10f;
    private const float StatusGap = 12f;
    private const float BarHeight = 8f;
    private const float BarGap = 8f;
    private const float HeaderFooterGap = 16f;
    private const int TileCount = 3;

    private enum HeroBody : byte { Status, Flag, Mark }

    private static uint cachedTerritoryId = uint.MaxValue;
    private static string cachedZoneName = string.Empty;
    private static CachedText progressText;

    public static void Draw(AutoHuntController controller)
    {
        var paused = controller.Paused;
        var (accent, accentSoft, label) = PhasePalette(controller);

        DrawHeaderStrip(CurrentZoneName(), accent, accentSoft, paused);
        Styling.VSpace(6f);
        DrawHeroCard(controller, accent, accentSoft, label);

        Styling.VSpace(10f);
        DrawStatTiles(controller);
    }

    private static void DrawHeaderStrip(string footer, Vector4 accent, Vector4 accentSoft, bool paused)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var available = ImGui.GetContentRegionAvail().X;
        var lineHeight = ImGui.GetTextLineHeight();
        var midY = origin.Y + lineHeight * 0.5f;

        var dot = paused ? accent : Styling.PulseColor(accent, accentSoft, Styling.PulseMedium);
        var radius = 4f * scale;
        Paint.Dot(drawList, new Vector2(origin.X + radius + 3f * scale, midY), radius, dot);

        var status = paused ? Loc.T(L.Shell.StatusPaused) : Loc.T(L.Shell.StatusRunning);
        var statusSize = TextDraw.SmallCapsSize(status);
        var statusX = origin.X + radius * 2f + 12f * scale;
        TextDraw.SmallCaps(status, new Vector2(statusX, midY - statusSize.Y * 0.5f), Styling.TextSecondary);

        using (Fonts.PushCaption())
        {
            var footerLeft = statusX + statusSize.X + HeaderFooterGap * scale;
            var footerText = TextDraw.Truncate(footer, origin.X + available - footerLeft);
            var footerSize = TextDraw.Measure(footerText);
            TextDraw.At(footerText, new Vector2(origin.X + available - footerSize.X, midY - footerSize.Y * 0.5f), Styling.TextMuted);
        }

        ImGui.Dummy(new Vector2(available, lineHeight));
    }

    // The card is as tall as its lines at the current font sizes, and every line is cut to the column, so nothing
    // spills past its border whatever size the fonts are set to.
    private static void DrawHeroCard(AutoHuntController controller, Vector4 accent, Vector4 accentSoft, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var progress = controller.Progress;
        var sRank = controller.SessionSnapshot?.IsSRank == true;
        CurrentFlag.View flag = default;
        var body = CurrentMark.TryGet(controller, out var mark) ? HeroBody.Mark
            : !sRank && CurrentFlag.TryGet(controller, out flag) ? HeroBody.Flag
            : HeroBody.Status;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, HeroCardHeight(body));
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var active = controller.Running && !controller.Paused;
        var rounding = Styling.PanelRounding * scale;

        Paint.Glass(drawList, origin, end, rounding, accent, active ? 0.10f : 0.03f, 0f, elevated: true);
        if (active)
        {
            Paint.Stroke(drawList, origin, end, Styling.PulseColor(Styling.WithAlpha(accent, 0.5f), accentSoft, Styling.PulseMedium), rounding, 1.6f);
        }

        var padX = PadX * scale;
        var ringRadius = size.Y * 0.5f - RingInset * scale;
        var ringCenter = new Vector2(origin.X + padX + ringRadius, origin.Y + size.Y * 0.5f);
        DrawRideRing(ringCenter, ringRadius, accent, active, progress, sRank);

        var columnX = ringCenter.X + ringRadius + RingGap * scale;
        var columnWidth = end.X - padX - columnX;
        var y = origin.Y + CardPadY * scale;

        y += DrawPhaseChip(columnX, y, columnWidth, label, accent, accentSoft) + ChipGap * scale;
        y = body switch
        {
            HeroBody.Mark => DrawMark(mark, controller.Status, columnX, columnWidth, y),
            HeroBody.Flag => DrawFlag(flag, controller.Status, columnX, columnWidth, y),
            _ => DrawStatus(progress.CatchingUp ? ReadyState.CatchUpLine(progress) : controller.Status, columnX, columnWidth, y),
        };

        // While a mark is fought the bar is its health.
        var barHeight = BarHeight * scale;
        var barOrigin = new Vector2(columnX, y);
        if (active && progress.HasMarkHealth)
        {
            Paint.Bar(drawList, barOrigin, columnWidth, barHeight, progress.MarkHealth, accent);
        }
        else if (active)
        {
            Paint.IndeterminateBar(drawList, barOrigin, columnWidth, barHeight, accent);
        }
        else
        {
            Paint.Bar(drawList, barOrigin, columnWidth, barHeight, 0f, accent);
        }

        y += barHeight + BarGap * scale;
        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(ProgressLine(progress, sRank), columnWidth), new Vector2(columnX, y), Styling.WithAlpha(accentSoft, 0.9f));
            if (!sRank)
            {
                y += ImGui.GetTextLineHeight() + ConductorLineGap * scale;
                TextDraw.At(TextDraw.Truncate(ConductorLine.Get(progress), columnWidth), new Vector2(columnX, y), Styling.TextDim);
            }
        }

        ImGui.Dummy(size);
    }

    // Adds up the same lines and gaps the card draws, so the two stay in step.
    private static float HeroCardHeight(HeroBody body)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var bodyLine = ImGui.GetTextLineHeight();
        float captionLine;
        using (Fonts.PushCaption())
        {
            captionLine = ImGui.GetTextLineHeight();
        }

        var chip = captionLine + ChipPadY * 2f * scale;
        var bodyBlock = body == HeroBody.Status
            ? bodyLine + StatusGap * scale
            : bodyLine + SubLineGap * scale + captionLine + BodyGap * scale;
        var gaps = (CardPadY * 2f + ChipGap + BarHeight + BarGap + ConductorLineGap) * scale;
        return gaps + chip + bodyBlock + captionLine * 2f;
    }

    private static float DrawStatus(string status, float x, float width, float y)
    {
        var text = TextDraw.Truncate(string.IsNullOrWhiteSpace(status) ? Loc.T(L.Common.Working) : status, width);
        TextDraw.At(text, new Vector2(x, y), Styling.TextSecondary);
        return y + ImGui.GetTextLineHeight() + StatusGap * ImGuiHelpers.GlobalScale;
    }

    private static float DrawMark(in CurrentMark.View mark, string status, float x, float width, float y)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var lineHeight = ImGui.GetTextLineHeight();
        var nameX = x + DrawRankBadge(mark.Rank, x, y + lineHeight * 0.5f);
        TextDraw.At(TextDraw.Truncate(mark.Name, x + width - nameX), new Vector2(nameX, y), Styling.TextStrong);
        y += lineHeight + SubLineGap * scale;

        using (Fonts.PushCaption())
        {
            var zone = TextDraw.Truncate(mark.ZoneName, width);
            TextDraw.At(zone, new Vector2(x, y), Styling.TextDim);
            TextDraw.Trailing(status, x + TextDraw.Measure(zone).X, x + width, y, Styling.TextMuted);
            y += ImGui.GetTextLineHeight();
        }

        return y + BodyGap * scale;
    }

    private static float DrawFlag(in CurrentFlag.View flag, string status, float x, float width, float y)
    {
        var scale = ImGuiHelpers.GlobalScale;
        TextDraw.At(TextDraw.Truncate(flag.Line, width), new Vector2(x, y), Styling.TextStrong);
        y += ImGui.GetTextLineHeight() + SubLineGap * scale;

        using (Fonts.PushCaption())
        {
            TextDraw.At(TextDraw.Truncate(status, width), new Vector2(x, y), Styling.TextMuted);
            y += ImGui.GetTextLineHeight();
        }

        return y + BodyGap * scale;
    }

    private static float DrawRankBadge(HuntMarkRank? rank, float leftX, float midY)
    {
        if (rank is not { } markRank)
        {
            return 0f;
        }

        return RankBadge.Draw(ImGui.GetWindowDrawList(), markRank, leftX, midY, InlineRankSize) + RankGap * ImGuiHelpers.GlobalScale;
    }

    // The ring fills with the marks credited once the ride knows how many its expansion has; until then it only shows
    // that the ride is moving.
    private static void DrawRideRing(Vector2 center, float radius, Vector4 accent, bool active, RideProgress progress, bool sRank)
    {
        var thickness = 6f * ImGuiHelpers.GlobalScale;
        ProgressRing.Track(center, radius, thickness, Styling.WithAlpha(Styling.BorderDim, 0.7f));
        var expected = sRank ? 1 : progress.ExpectedMarks;
        if (expected > 0)
        {
            ProgressRing.Fill(center, radius, thickness, progress.MarksCredited / (float)expected, Styling.WithAlpha(accent, 0.85f));
        }

        if (active)
        {
            ProgressRing.Sweep(center, radius, thickness, accent, Styling.PulseOrbit, MathF.PI * 0.6f, 1f);
        }

        ProgressRing.CenterIcon(center, sRank ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Train, Styling.TextDim, radius * 0.55f);
    }

    // Flags followed and marks credited, rebuilt only when a count changes.
    private static string ProgressLine(RideProgress progress, bool sRank)
    {
        if (sRank)
        {
            return CreditedLine.Get(progress);
        }

        var flags = progress.FlagsFollowed;
        var key = ((long)flags << 32) | (uint)progress.MarksCredited;
        if (progressText.TryGet(key, out var line))
        {
            return line;
        }

        return progressText.Set(key, Loc.T(L.Ride.ProgressLine, Loc.Plural(L.Ride.FlagsFollowed, flags), CreditedLine.Get(progress)));
    }

    private static float DrawPhaseChip(float x, float y, float maxWidth, string text, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var padX = ChipPadX * scale;
        var padY = ChipPadY * scale;

        using (Fonts.PushCaption())
        {
            var label = TextDraw.Truncate(TextDraw.Upper(text), maxWidth - padX * 2f);
            var textSize = new Vector2(TextDraw.Measure(label).X, ImGui.GetTextLineHeight());
            var chipMin = new Vector2(x, y);
            var chipMax = chipMin + new Vector2(padX * 2f + textSize.X, textSize.Y + padY * 2f);
            Paint.Pill(drawList, chipMin, chipMax, Styling.WithAlpha(accent, 0.28f), Styling.WithAlpha(accent, 0.65f));
            TextDraw.At(label, new Vector2(x + padX, y + padY), accentSoft);
            return chipMax.Y - chipMin.Y;
        }
    }

    private static (Vector4 Accent, Vector4 AccentSoft, string Label) PhasePalette(AutoHuntController controller)
    {
        if (!controller.Running)
        {
            return (Styling.TextDim, Styling.TextSecondary, Loc.T(L.Run.PhaseReady));
        }

        if (controller.Paused)
        {
            return controller.PauseReason == PauseReason.InContent
                ? (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePausedInContent))
                : (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePaused));
        }

        return controller.Phase switch
        {
            HuntPhase.Fighting  => (Styling.AccentGlow, Styling.AccentGlowSoft, Loc.T(L.Run.PhaseFighting)),
            HuntPhase.Finishing => (Styling.AccentMint,  Styling.AccentMintSoft,  Loc.T(L.Run.PhaseFinishing)),
            HuntPhase.Idle      => (Styling.TextDim,     Styling.TextSecondary,   Loc.T(L.Run.PhaseStandingBy)),
            HuntPhase.Waiting   => (Styling.TextDim,     Styling.TextSecondary,   ReadyState.ActivityLabel(controller)),
            _                   => (Styling.AccentBlue,  Styling.AccentBlueSoft,  PhaseChipLabel(controller)),
        };
    }

    // The chip keeps to the phase's short name; the status line under it says where a catch-up is.
    private static string PhaseChipLabel(AutoHuntController controller)
        => controller.Progress.CatchingUp ? ReadyState.RidePhaseLabel(RidePhase.CatchingUp) : ReadyState.ActivityLabel(controller);

    private static void DrawStatTiles(AutoHuntController controller)
    {
        var session = controller.SessionSnapshot;
        var scale = ImGuiHelpers.GlobalScale;
        var gap = 8f * scale;
        var tileWidth = (ImGui.GetContentRegionAvail().X - gap * (TileCount - 1)) / TileCount;
        var nuts = session?.Nuts ?? 0;

        StatTile.Draw(Loc.T(L.Run.TileMarks), (session?.MarksCredited ?? 0).ToString(Loc.Culture), null, Styling.AccentGlow, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileSeals), (session?.Seals ?? 0).ToString("N0", Loc.Culture), nuts > 0 ? Loc.T(L.Run.NutsSub, nuts) : null, Styling.AccentAmber, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileElapsed), Formatting.Elapsed(session?.Elapsed ?? TimeSpan.Zero), null, Styling.AccentBlue, tileWidth);
    }

    private static string CurrentZoneName()
    {
        uint territoryId = Svc.ClientState.TerritoryType;
        if (territoryId == cachedTerritoryId)
        {
            return cachedZoneName;
        }

        cachedTerritoryId = territoryId;
        cachedZoneName = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.PlaceName.ValueNullable?.Name.ExtractText() ?? string.Empty;
        if (cachedZoneName.Length == 0)
        {
            cachedZoneName = Loc.T(L.Run.SomewhereElse);
        }

        return cachedZoneName;
    }
}
