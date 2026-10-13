using AutoHuntTrain.Core.Game.Ops;
using AutoHuntTrain.Core.Ipc;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Tasks;

public abstract partial class AutoCommon
{
    // A tag that never goes off while the mark is in reach, for facing, sight or a refused cast, would leave the mark
    // uncredited, so after this long in reach without one the full preset takes the mark over.
    private const int MarkTagStallMs = 15_000;
    // A tag's approach stops this far inside its attack's reach, as the ranged approach does inside the 25 yalm spells.
    private const float MarkTagApproachInsetMeters = 5f;

    // The attack this plugin casts itself to tag the mark in hand; none while the full preset fights it.
    private TagAction markTag;
    private uint tagUnavailableLoggedJob;

    private string MarkPresetName => markTag.IsValid ? HuntCombatPreset.TagName : HuntCombatPreset.Name;

    // Settled before the approach, since a tag is fought from its attack's reach and not from melee.
    private void ResolveMarkTag(string scope)
    {
        markTag = default;
        if (!Plugin.Instance.Configuration.TagWithRangedAttacks || Svc.Objects.LocalPlayer is not { } player)
        {
            return;
        }

        var jobId = player.ClassJob.RowId;
        var tag = TagActions.Resolve(jobId, player.Level);
        if (!tag.IsValid)
        {
            LogTagUnavailable(jobId, player.Level);
            return;
        }

        markTag = tag;
        Diag($"{scope}: tagging with {tag.Name} (action {tag.ActionId}) from up to {tag.ReachMeters:F0}m on the '{HuntCombatPreset.TagName}' preset");
    }

    private void LogTagUnavailable(uint jobId, byte level)
    {
        if (tagUnavailableLoggedJob == jobId)
        {
            return;
        }

        tagUnavailableLoggedJob = jobId;
        var job = Svc.Objects.LocalPlayer?.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? $"job {jobId}";
        Warn(TagActions.HasLadder(jobId)
            ? $"Hunt: tagging is on, but {job} at level {level} has not learned its tag attack yet; marks are fought with the full '{HuntCombatPreset.Name}' preset"
            : $"Hunt: tagging is on, but {job} has no single-target attack at range to tag with; marks are fought with the full '{HuntCombatPreset.Name}' preset");
    }

    // Casts the tag whenever the game says it is ready. False once it has stayed quiet in reach for MarkTagStallMs.
    private bool TagMark(ref MarkTagging tagging, ulong markId, bool inReach, long now, string scope)
    {
        if (TagActions.TryUse(markTag.ActionId, markId, out var status))
        {
            if (tagging.Casts == 0)
            {
                Diag($"{scope}: first {markTag.Name} sent at the mark");
            }

            tagging.Casts++;
            tagging.QuietSince = now;
            return true;
        }

        tagging.LastStatus = status;
        if (!TagActions.IsRoutine(status) && status != tagging.LoggedStatus)
        {
            tagging.LoggedStatus = status;
            Diag($"{scope}: {markTag.Name} is held back by the game (status {status}, mark {(inReach ? "in" : "out of")} reach)");
        }

        if (!inReach)
        {
            tagging.QuietSince = now;
            return true;
        }

        return now - tagging.QuietSince < MarkTagStallMs;
    }

    private struct MarkTagging
    {
        public string? ActionName;
        public long QuietSince;
        public int Casts;
        public uint LastStatus;
        public uint LoggedStatus;
    }
}
