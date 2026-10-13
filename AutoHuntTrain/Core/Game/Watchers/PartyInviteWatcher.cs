using AutoHuntTrain.Core.Game.Ops;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace AutoHuntTrain.Core.Game.Watchers;

// The game asks "Join X's party?" when a player invites the character. On a train the invite is taken, since party
// members share the credit on a mark, and only while a ride runs; any other invite is left for the player. The prompt
// is matched against its Addon sheet template, so the check works in every client language, and the addon is looked
// up again by id right before it is clicked.
internal sealed unsafe class PartyInviteWatcher : IDisposable
{
    private const ushort NoInvite = 0;
    // A prompt clicked the frame it opens can drop the click, so the accept waits at least this long even with the
    // humanizer off.
    private const int MinimumAcceptDelayMs = 500;
    private const int NotReadyAbandonMs = 15_000;

    private ushort inviteAddonId = NoInvite;
    private long acceptAtTick;
    private string inviterName = "";
    private string inviterWorld = "";

    public PartyInviteWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, SelectYesnoPrompt.AddonName, OnSelectYesnoSetup);
        Svc.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, SelectYesnoPrompt.AddonName, OnSelectYesnoSetup);
    }

    private static bool AcceptArmed()
    {
        var plugin = Plugin.Instance;
        return plugin.Configuration.AcceptPartyInvites
            && plugin.Controller.OpenToParty
            && !PartyOps.InParty();
    }

    private void OnSelectYesnoSetup(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null || inviteAddonId != NoInvite || !AcceptArmed())
        {
            return;
        }

        var prompt = SelectYesnoPrompt.Read(addon);
        if (!PartyPrompts.JoinParty.Matches(prompt))
        {
            RunLog.Debug($"SelectYesno {addon->Id} is not a party invite: \"{prompt}\"");
            return;
        }

        CaptureInviter();
        inviteAddonId = addon->Id;
        var delayMs = Math.Max(MinimumAcceptDelayMs, Humanizer.DrawDelayMs(HumanAction.AcceptInvite));
        acceptAtTick = Environment.TickCount64 + delayMs;
        RunLog.Info($"Party invite from {DisplayName()} during the ride; accepting it.");
        RunLog.Debug($"Party invite: waiting {delayMs} ms before {Humanizer.Describe(HumanAction.AcceptInvite)}");
    }

    private void OnUpdate(IFramework _)
    {
        if (inviteAddonId == NoInvite)
        {
            return;
        }

        var addon = SelectYesnoPrompt.Find(inviteAddonId);
        if (addon is null)
        {
            RunLog.Debug("Party invite: the prompt closed before the accept; standing down.");
            inviteAddonId = NoInvite;
            return;
        }

        var now = Environment.TickCount64;
        if (now < acceptAtTick)
        {
            return;
        }

        if (!AcceptArmed())
        {
            RunLog.Info($"Party invite from {DisplayName()} left for you to answer: the ride is no longer open to a party.");
            inviteAddonId = NoInvite;
            return;
        }

        if (!GenericHelpers.IsAddonReady(addon))
        {
            if (now >= acceptAtTick + NotReadyAbandonMs)
            {
                RunLog.Warning($"Party invite: prompt {addon->Id} never became ready; standing down.");
                inviteAddonId = NoInvite;
            }

            return;
        }

        inviteAddonId = NoInvite;
        try
        {
            new AddonMaster.SelectYesno((nint)addon).Yes();
            RunLog.Info($"Accepted the party invite from {DisplayName()}.");
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Party invite: the accept click threw.");
        }
    }

    private void CaptureInviter()
    {
        inviterName = "";
        inviterWorld = "";
        try
        {
            var proxy = InfoProxyPartyInvite.Instance();
            if (proxy is null)
            {
                return;
            }

            inviterName = proxy->InviterName.ToString();
            var world = Svc.Data.GetExcelSheet<World>().GetRowOrDefault(proxy->InviterWorldId);
            inviterWorld = world?.Name.ExtractText() ?? "";
        }
        catch (Exception exception)
        {
            RunLog.Debug($"Party invite: failed to read the inviter: {exception.Message}");
        }
    }

    private string DisplayName()
        => string.IsNullOrEmpty(inviterName) ? "a player"
         : string.IsNullOrEmpty(inviterWorld) ? inviterName
         : $"{inviterName}@{inviterWorld}";
}
