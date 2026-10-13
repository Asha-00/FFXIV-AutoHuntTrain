using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoHuntTrain.Windows.Pages;

internal sealed class HistoryPage
{
    private const int ChartRuns = 24;
    private const int TileCount = 4;
    private const float PadX = 14f;
    private const float MetricWidth = 64f;
    private const float IconColumn = 18f;
    private const float ConfirmSlide = 12f;
    private const string NoValue = "-";
    private const float BadgeGap = 6f;

    private bool confirmClear;
    private CachedText finishedText;
    private CachedText perRideText;
    private CachedText nutsText;
    private CachedText topWorldText;

    public void Draw(Plugin plugin)
    {
        var history = plugin.History;
        var totals = history.Lifetime;
        var subtitle = history.Records.Count == 0
            ? Loc.T(L.History.Empty)
            : Loc.Plural(L.History.Summary, history.Records.Count, Formatting.Elapsed(totals.Duration), totals.MarksPerHour.ToString("F1", Loc.Culture));
        PageHeader.Draw(Loc.T(L.History.Title), subtitle);

        DrawLifetime(totals, history.Revision);
        Styling.VSpace(14f);

        if (history.Records.Count == 0)
        {
            DrawEmptyState();
            return;
        }

        Label(Loc.T(L.History.MarksPerRun));
        DrawChart(history);
        Styling.VSpace(12f);

        Label(Loc.T(L.History.RecentRuns));
        var records = history.Records;
        for (var index = 0; index < records.Count; index++)
        {
            DrawRow(records[index], index);
            Styling.VSpace(2f);
        }

        Styling.VSpace(8f);
        DrawClearControl(history);
    }

    private static void Label(string text)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var size = TextDraw.SectionTitleSize(text);
        TextDraw.SectionTitle(text, origin, Styling.TextStrong);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, size.Y + 8f * scale));
    }

    // The captions are rebuilt only when the history changes; the revision is their cache key.
    private void DrawLifetime(RunHistory.LifetimeTotals totals, int revision)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = 8f * scale;
        var tileWidth = (ImGui.GetContentRegionAvail().X - gap * (TileCount - 1)) / TileCount;
        var finished = totals.Runs > 0 ? finishedText.Get(revision, static _ => Loc.T(L.History.TileFinished, Plugin.Instance.History.Lifetime.Finished)) : null;
        var perRide = totals.Runs > 0 ? perRideText.Get(revision, static _ => Loc.T(L.History.TilePerRide, Plugin.Instance.History.Lifetime.MarksPerRun.ToString("F1", Loc.Culture))) : null;
        var nuts = totals.Nuts > 0 ? nutsText.Get(revision, static _ => Loc.Plural(L.History.TileNutsCount, Plugin.Instance.History.Lifetime.Nuts)) : null;
        var topWorld = totals.TopWorld is null ? NoValue : topWorldText.Get(revision, static _ => WhereText(Plugin.Instance.History.Lifetime.TopWorld!, Plugin.Instance.History.Lifetime.TopDataCenter ?? string.Empty));
        var topGroup = totals.TopGroup is { } group ? GroupLabels.Name(group) : null;

        StatTile.Draw(Loc.T(L.History.TileRuns), totals.Runs.ToString("N0", Loc.Culture), finished, Styling.AccentGlow, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.History.TileMarks), totals.Marks.ToString("N0", Loc.Culture), perRide, Styling.AccentBlue, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.History.TileSeals), totals.Seals.ToString("N0", Loc.Culture), nuts, Styling.AccentAmber, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.History.TileTopWorld), topWorld, topGroup, Styling.AccentNebula, tileWidth);
    }

    private static string WhereText(string world, string dataCenter)
    {
        if (world.Length == 0)
        {
            return NoValue;
        }

        return dataCenter.Length == 0 ? world : Loc.T(L.History.WorldAndDataCenter, world, dataCenter);
    }

    private static void DrawEmptyState()
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, 110f * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);

        var center = new Vector2((origin.X + end.X) * 0.5f, origin.Y + size.Y * 0.42f);
        ProgressRing.CenterIcon(center, FontAwesomeIcon.History, Styling.TextMuted, 26f * scale);
        TextDraw.Center(Loc.T(L.History.NoRuns), center.X, center.Y + 22f * scale, Styling.TextMuted);
        ImGui.Dummy(size);
    }

    private static void DrawChart(RunHistory history)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var records = history.Records;
        var count = Math.Min(ChartRuns, records.Count);
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.ChartHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();

        Paint.Surface(drawList, origin, end, Styling.CardRounding * scale, Styling.WithAlpha(Styling.Surface0, 0.6f), Styling.WithAlpha(Styling.BorderDim, 0.5f), topLight: false);

        var padX = PadX * scale;
        var plotMin = new Vector2(origin.X + padX, origin.Y + 28f * scale);
        var plotMax = new Vector2(end.X - padX, end.Y - 12f * scale);
        var plotWidth = plotMax.X - plotMin.X;
        var plotHeight = plotMax.Y - plotMin.Y;

        var peak = 1;
        for (var index = 0; index < count; index++)
        {
            peak = Math.Max(peak, records[index].MarksCredited);
        }

        using (Fonts.PushCaption())
        {
            TextDraw.At(Loc.Plural(L.History.ChartRange, count), new Vector2(plotMin.X, origin.Y + 9f * scale), Styling.TextMuted);
            TextDraw.Right(Loc.T(L.History.ChartPeak, peak), plotMax.X, origin.Y + 9f * scale, Styling.TextMuted);
        }

        drawList.AddLine(new Vector2(plotMin.X, plotMax.Y), new Vector2(plotMax.X, plotMax.Y), Paint.Col(Styling.WithAlpha(Styling.BorderDim, 0.7f)), 1f);

        var gap = 2f * scale;
        var barWidth = MathF.Max(2f * scale, (plotWidth - gap * (count - 1)) / count);
        var stride = barWidth + gap;
        var hovered = -1;
        if (Hit.HoveringRect(plotMin, plotMax))
        {
            hovered = (int)((ImGui.GetMousePos().X - plotMin.X) / stride);
            if (hovered >= count)
            {
                hovered = -1;
            }
        }

        var rounding = MathF.Min(4f * scale, barWidth * 0.5f);
        for (var index = 0; index < count; index++)
        {
            var record = records[count - 1 - index];
            var height = MathF.Max(2f * scale, plotHeight * record.MarksCredited / peak);
            var barMin = new Vector2(plotMin.X + stride * index, plotMax.Y - height);
            var barMax = new Vector2(barMin.X + barWidth, plotMax.Y);
            var color = index == hovered ? Styling.AccentGlowSoft : Styling.AccentGlow;
            drawList.AddRectFilled(barMin, barMax, Paint.Col(color), rounding, ImDrawFlags.RoundCornersTop);
        }

        if (hovered >= 0)
        {
            Tooltip.Show(ChartTooltip(records[count - 1 - hovered]));
        }

        ImGui.Dummy(size);
    }

    private static string ChartTooltip(RunRecord record)
        => Loc.T(L.History.ChartTooltip, RelativeTime(record.EndedAtUtc), record.MarksCredited, Formatting.Elapsed(record.Duration));

    private static void DrawRow(RunRecord record, int index)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.HistoryRowHeight * scale);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();

        ImGui.PushID((nint)(index + 1));
        var hit = Hit.Area("##run", size, handCursor: false);
        var hover = Motion.Hover(Motion.Key("##run"), hit.Hovered);
        ImGui.PopID();

        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentGlow, 0.02f, hover);

        var padX = PadX * scale;
        var midY = origin.Y + size.Y * 0.5f;
        var textX = DrawRunIcon(record, origin.X + padX, midY);
        var when = RelativeTime(record.EndedAtUtc);
        var job = string.IsNullOrEmpty(record.JobAbbreviation) ? NoValue : record.JobAbbreviation;
        var detail = Loc.T(L.History.RowDetail, WhereText(record.WorldName, record.DataCenterName), job, Formatting.Elapsed(record.Duration));

        var whenSize = TextDraw.Measure(when);
        Vector2 detailSize;
        using (Fonts.PushCaption())
        {
            detailSize = TextDraw.Measure(detail);
        }

        var top = midY - (whenSize.Y + 3f * scale + detailSize.Y) * 0.5f;
        TextDraw.At(when, new Vector2(textX, top), Styling.TextStrong);
        DrawRowBadges(drawList, record, textX + whenSize.X + BadgeGap * 2f * scale, top + whenSize.Y * 0.5f);
        using (Fonts.PushCaption())
        {
            TextDraw.At(detail, new Vector2(textX, top + whenSize.Y + 3f * scale), Styling.TextDim);
        }

        var metricWidth = MetricWidth * scale;
        var x = end.X - padX - metricWidth;
        DrawMetric(x, midY, metricWidth, Metric(record.Nuts), Loc.T(L.History.TileNuts), record.Nuts > 0 ? Styling.AccentNebula : Styling.TextMuted);
        x -= metricWidth;
        DrawMetric(x, midY, metricWidth, Metric(record.Seals), Loc.T(L.History.TileSeals), record.Seals > 0 ? Styling.AccentAmber : Styling.TextMuted);
        x -= metricWidth;
        DrawMetric(x, midY, metricWidth, record.MarksCredited.ToString(Loc.Culture), Loc.T(L.History.TileMarks), Styling.AccentBlue);

        if (hit.Hovered)
        {
            Tooltip.Show(RunTooltip(record));
        }
    }

    private static void DrawRowBadges(ImDrawListPtr drawList, RunRecord record, float x, float midY)
    {
        var gap = BadgeGap * ImGuiHelpers.GlobalScale;
        if (record.Outcome is { } outcome)
        {
            x += Badge.DrawLeft(drawList, OutcomeLabel(outcome), OutcomeColor(outcome), x, midY) + gap;
        }

        if (record.IsSRank)
        {
            x += Badge.DrawLeft(drawList, Loc.T(L.Feed.SHunt), Styling.AccentNebula, x, midY) + gap;
        }

        if (record.ResolveGroup() is { } group)
        {
            Badge.DrawLeft(drawList, GroupLabels.Name(group), GroupLabels.Color(group), x, midY);
        }
    }

    private static string OutcomeLabel(RideOutcome outcome) => outcome switch
    {
        RideOutcome.AllCredited => Loc.T(L.History.OutcomeAllCredited),
        RideOutcome.ConductorQuiet => Loc.T(L.History.OutcomeConductorQuiet),
        RideOutcome.Stopped => Loc.T(L.History.OutcomeStopped),
        RideOutcome.Abandoned => Loc.T(L.History.OutcomeAbandoned),
        _ => Loc.T(L.History.OutcomeFaulted),
    };

    private static Vector4 OutcomeColor(RideOutcome outcome) => outcome switch
    {
        RideOutcome.AllCredited => Styling.AccentMint,
        RideOutcome.ConductorQuiet => Styling.AccentBlue,
        RideOutcome.Stopped => Styling.TextDim,
        RideOutcome.Abandoned => Styling.AccentAmber,
        _ => Styling.AccentRose,
    };

    // Returns where the row's text starts; the icon is centred in a fixed column so every row's text lines up.
    private static float DrawRunIcon(RunRecord record, float x, float midY)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var column = IconColumn * scale;
        var icon = record.IsSRank ? FontAwesomeIcon.Crosshairs : FontAwesomeIcon.Train;
        var iconSize = TextDraw.IconSize(icon);
        TextDraw.Icon(icon, new Vector2(x + (column - iconSize.X) * 0.5f, midY - iconSize.Y * 0.5f), Styling.TextDim);
        return x + column + 10f * scale;
    }

    private static string Metric(int value) => value > 0 ? value.ToString("N0", Loc.Culture) : NoValue;

    private static void DrawMetric(float x, float midY, float width, string value, string label, Vector4 color)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var valueSize = TextDraw.Measure(value);
        var labelSize = TextDraw.SmallCapsSize(label);
        var top = midY - (valueSize.Y + 2f * scale + labelSize.Y) * 0.5f;
        var centerX = x + width * 0.5f;
        TextDraw.At(value, new Vector2(centerX - valueSize.X * 0.5f, top), color);
        TextDraw.SmallCaps(label, new Vector2(centerX - labelSize.X * 0.5f, top + valueSize.Y + 2f * scale), Styling.TextMuted);
    }

    private static string RunTooltip(RunRecord record)
    {
        var lines = record.EndedAtUtc.ToLocalTime().ToString("g", Loc.Culture);
        if (record.MarksPerHour > 0)
        {
            lines += "\n" + Loc.T(L.History.TooltipRate, record.MarksPerHour.ToString("F1", Loc.Culture));
        }

        if (record.WorldName.Length > 0)
        {
            lines += "\n" + Loc.T(L.History.TooltipWorld, record.WorldName, record.DataCenterName.Length == 0 ? NoValue : record.DataCenterName);
        }

        if (record.CrossedDataCenter)
        {
            lines += "\n" + Loc.T(L.History.TooltipCrossed);
        }

        return lines;
    }

    private static string RelativeTime(DateTime utc)
    {
        var span = DateTime.UtcNow - utc;
        if (span.TotalSeconds < 60)
        {
            return Loc.T(L.History.JustNow);
        }

        if (span.TotalMinutes < 60)
        {
            return Loc.T(L.History.MinutesAgo, (int)span.TotalMinutes);
        }

        if (span.TotalHours < 24)
        {
            return Loc.T(L.History.HoursAgo, (int)span.TotalHours);
        }

        return span.TotalDays < 7 ? Loc.T(L.History.DaysAgo, (int)span.TotalDays) : utc.ToLocalTime().ToString("M", Loc.Culture);
    }

    private void DrawClearControl(RunHistory history)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var available = ImGui.GetContentRegionAvail().X;
        var origin = ImGui.GetCursorScreenPos();
        var reveal = Motion.Transition(Motion.Key("##aht_hist_clear_state"), confirmClear);
        var slide = (1f - reveal) * ConfirmSlide * scale;
        using var alpha = Motion.PushAlpha(reveal);

        if (!confirmClear)
        {
            var label = Loc.T(L.History.ClearHistory);
            ImGui.SetCursorScreenPos(new Vector2(origin.X + available - PillButton.Width(label, FontAwesomeIcon.Trash) - slide, origin.Y));
            if (PillButton.Draw("##aht_hist_clear", label, Styling.AccentRose, PillButton.Emphasis.Ghost, FontAwesomeIcon.Trash))
            {
                confirmClear = true;
            }

            return;
        }

        var question = Loc.T(L.History.ClearQuestion);
        var yes = Loc.T(L.History.ClearYes);
        var no = Loc.T(L.Common.Cancel);
        var questionSize = TextDraw.Measure(question);
        var yesWidth = PillButton.Width(yes);
        var noWidth = PillButton.Width(no);
        var gap = 8f * scale;
        var buttonHeight = 28f * scale;

        var x = origin.X + available - noWidth - slide;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (PillButton.Draw("##aht_hist_clear_no", no, Styling.AccentGlow, PillButton.Emphasis.Ghost))
        {
            confirmClear = false;
        }

        x -= gap + yesWidth;
        ImGui.SetCursorScreenPos(new Vector2(x, origin.Y));
        if (PillButton.Draw("##aht_hist_clear_yes", yes, Styling.AccentRose, PillButton.Emphasis.Filled))
        {
            history.Clear();
            confirmClear = false;
        }

        TextDraw.At(question, new Vector2(x - gap * 1.5f - questionSize.X, origin.Y + (buttonHeight - questionSize.Y) * 0.5f), Styling.AccentRoseSoft);
    }
}
