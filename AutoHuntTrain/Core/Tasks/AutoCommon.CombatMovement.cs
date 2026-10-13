using AutoHuntTrain.Core.Ipc;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

public abstract partial class AutoCommon
{
    // Two frames give the combat plugin one update on the ground to drop the target it was steering toward.
    private const int CombatMovementSettleFrames = 2;

    private bool dodgeTuningRefusalLogged;

    protected void HoldCombatMovement(string scope)
    {
        if (BossModIPC.Instance.HoldMovement())
        {
            Diag($"{scope}: the combat plugin's automatic movement is paused while this plugin drives the character");
        }
    }

    // Taken on the ground at the start of a run: while the character flies, the combat plugin never recomputes, so a
    // hold taken in the air leaves its stale target in place until the next landing.
    protected async Task HoldCombatMovementAndSettle(string scope)
    {
        HoldCombatMovement(scope);
        if (!CancelToken.IsCancellationRequested)
        {
            await NextFrame(CombatMovementSettleFrames);
        }
    }

    protected void ReleaseCombatMovement(string scope)
    {
        if (BossModIPC.Instance.ReleaseMovement())
        {
            Diag($"{scope}: the combat plugin's automatic movement is back to your own setting");
        }
    }

    // Rolled once as a mark's hunt begins and never mid-fight, so no track flips while the character is dodging. Every
    // bundled preset takes the same roll, so it holds whichever one fights the mark.
    private void TuneDodging(string markName)
    {
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.CombatMovementEnabled)
        {
            DodgeTuning.Release();
            return;
        }

        // An override that cannot be cleared would outlast the ride, so none is set.
        if (!BossModIPC.Instance.CanClearTransientStrategy)
        {
            LogDodgeTuningRefused("the combat plugin cannot take a preset override back");
            return;
        }

        var roll = DodgeTuning.Roll(configuration);
        var presets = HuntCombatPreset.Names;
        var tuned = 0;
        for (var presetIndex = 0; presetIndex < presets.Length; presetIndex++)
        {
            if (DodgeTuning.Apply(presets[presetIndex], roll))
            {
                tuned++;
                continue;
            }

            LogDodgeTuningRefused($"the '{presets[presetIndex]}' preset has no NormalMovement module, or the combat plugin's build lacks these options");
        }

        if (tuned == 0)
        {
            return;
        }

        Diag($"Hunt: dodging for {markName} rolled dodge delay {roll.DodgeDelayOption}, movement delay {roll.MovementDelayOption}, extra room {roll.CushionOption} on {tuned} preset(s)");
    }

    private void LogDodgeTuningRefused(string reason)
    {
        if (dodgeTuningRefusalLogged)
        {
            return;
        }

        dodgeTuningRefusalLogged = true;
        Warn($"Hunt: dodge tuning is on but was not applied: {reason}; BossMod dodges as the preset is set up");
    }

    protected string DescribeOutsideMovement()
    {
        var bossMod = BossModIPC.Instance;
        return $"combat plugin navigating {bossMod.IsNavigating()}, forcing movement {bossMod.IsForcingMovement()}, movement hold {bossMod.MovementHeld}, {ConditionTag()}";
    }
}
