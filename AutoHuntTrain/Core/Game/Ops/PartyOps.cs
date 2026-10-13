using Dalamud.Game.ClientState.Conditions;
using ECommons;
using ECommons.Automation;
using ECommons.DalamudServices;
using ECommons.Throttlers;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;

namespace AutoHuntTrain.Core.Game.Ops;

internal static unsafe class PartyOps
{
    public const ushort NoPrompt = 0;

    private const string LeaveCommand = "/leave";
    private const string ShoutCommand = "/shout";
    private const string SelectYesnoAddonName = "SelectYesno";
    // The game ignores addon clicks repeated faster than this.
    private const int ClickThrottleMs = 500;
    private const string ConfirmThrottleKey = "AutoHuntTrain.Party.ConfirmLeave";

    // The party list holds only a party on this world; a cross-world party shows as a condition instead.
    public static bool InParty()
        => Svc.Party.Length > 1 || Svc.Condition[ConditionFlag.ParticipatingInCrossWorldPartyOrAlliance];

    // A prompt shows a player by name, followed by the home world for one from another world. The party list holds a
    // party on this world, the cross-world proxy one formed across worlds.
    public static bool HasMemberShownAs(string shownName)
    {
        var party = Svc.Party;
        for (var index = 0; index < party.Length; index++)
        {
            if (party[index] is { } member && ShowsPlayer(shownName, member.Name.TextValue, member.World.ValueNullable?.Name.ExtractText()))
            {
                return true;
            }
        }

        if (!InfoProxyCrossRealm.IsCrossRealmParty())
        {
            return false;
        }

        var worlds = Svc.Data.GetExcelSheet<World>();
        var count = InfoProxyCrossRealm.GetPartyMemberCount();
        for (var memberIndex = 0; memberIndex < count; memberIndex++)
        {
            var member = InfoProxyCrossRealm.GetGroupMember((uint)memberIndex);
            if (member is not null && ShowsPlayer(shownName, member->NameString, worlds.GetRowOrDefault((uint)member->HomeWorld)?.Name.ExtractText()))
            {
                return true;
            }
        }

        return false;
    }

    public static bool SendLeave() => TrySend(LeaveCommand);

    public static bool Shout(string text) => TrySend($"{ShoutCommand} {text}");

    // The yes/no prompt open right now, if any, so a later check can tell a prompt the leave raised from one left open.
    public static ushort OpenPromptId()
        => GenericHelpers.TryGetAddonByName<AtkUnitBase>(SelectYesnoAddonName, out var addon) ? addon->Id : NoPrompt;

    public static bool ConfirmPromptOpenedSince(ushort promptBefore)
    {
        if (!GenericHelpers.TryGetAddonByName<AtkUnitBase>(SelectYesnoAddonName, out var addon)
            || addon->Id == promptBefore
            || !GenericHelpers.IsAddonReady(addon)
            || !EzThrottler.Throttle(ConfirmThrottleKey, ClickThrottleMs))
        {
            return false;
        }

        new AddonMaster.SelectYesno((nint)addon).Yes();
        return true;
    }

    // The world can sit behind a cross-world icon the prompt text keeps or drops, so anything before it is skipped.
    private static bool ShowsPlayer(string shownName, string name, string? world)
    {
        if (name.Length == 0 || !shownName.StartsWith(name, StringComparison.Ordinal))
        {
            return false;
        }

        var rest = shownName.AsSpan(name.Length);
        var firstLetter = 0;
        while (firstLetter < rest.Length && !char.IsLetter(rest[firstLetter]))
        {
            firstLetter++;
        }

        rest = rest[firstLetter..];
        return rest.IsEmpty || (world is not null && rest.Equals(world, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TrySend(string line)
    {
        try
        {
            Chat.SendMessage(line);
            return true;
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Party: sending '{line}' failed.");
            return false;
        }
    }
}
