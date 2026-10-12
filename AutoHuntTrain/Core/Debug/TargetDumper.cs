using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.ClientState.Objects.Types;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoHuntTrain.Core.Debug;

// Logs every identifier and state bit needed to compare HuntAlerts' creatureNameId with Dalamud's live object and the
// hunt registry. The client-struct target supplies BaseId; the matching Dalamud wrapper supplies NameId, DataId, HP,
// position and combat state.
internal static unsafe class TargetDumper
{
    public static void Dump()
    {
        var territoryId = Svc.ClientState.TerritoryType;
        var territoryName = TerritoryNames.Of(territoryId);
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Territory: {territoryId} ({territoryName})");

        var nativeTarget = TargetSystem.Instance()->Target;
        if (nativeTarget == null)
        {
            Svc.Chat.Print($"{AhtConstants.LogPrefix} No target. Click an NPC or a hunt mark first, then re-run /aht target.");
            return;
        }

        var baseId = nativeTarget->BaseId;
        var nativeName = nativeTarget->NameString;
        var target = FindDalamudTarget((nint)nativeTarget);
        if (target is null)
        {
            var residentName = Svc.Data.GetExcelSheet<ENpcResident>()
                ?.GetRowOrDefault(baseId)?.Singular.ToString() ?? nativeName;
            Svc.Chat.Print($"{AhtConstants.LogPrefix} Target: Name=\"{residentName}\" BaseId={baseId}; no Dalamud object wrapper was found.");
            RunLog.Info($"Target dump: territory={territoryId} ({territoryName}) name='{residentName}' BaseId={baseId} wrapper=false");
            return;
        }

        var name = target.Name.TextValue;
        var nameId = target is ICharacter character ? character.NameId : 0u;
        var wrapperBaseId = target.BaseId;
        var gameObjectId = target.GameObjectId;
        var position = target.Position;
        var kind = target.ObjectKind;
        var battle = target as IBattleNpc;
        var currentHp = battle?.CurrentHp ?? 0u;
        var maxHp = battle?.MaxHp ?? 0u;
        var inCombat = battle is not null && (battle.StatusFlags & StatusFlags.InCombat) != 0;
        var pulled = battle is not null && maxHp > 0 && (currentHp < maxHp || inCombat);
        var registry = "Rank=none RegistryTerritory=0 ExpansionWide=False";
        var registryMatch = false;
        if (nameId > 0 && HuntMarkRegistry.TryGet(nameId, out var mark))
        {
            registryMatch = true;
            registry = $"Rank={mark.Rank} RegistryTerritory={mark.TerritoryId} ExpansionWide={HuntMarkRegistry.IsExpansionWideAt(HuntMarkRegistry.IndexOf(nameId))}";
        }

        Svc.Chat.Print($"{AhtConstants.LogPrefix} Target: Name=\"{name}\" NameId={nameId} BaseId={wrapperBaseId} NativeBaseId={baseId} GameObjectId=0x{gameObjectId:X}");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Kind={kind} Position={Format(position)} HP={currentHp}/{maxHp} InCombat={inCombat} Pulled={pulled}");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} RegistryMatch={registryMatch} {registry}");

        RunLog.Info(
            $"Target dump: territory={territoryId} ({territoryName}) name='{name}' NameId={nameId} BaseId={wrapperBaseId} NativeBaseId={baseId} "
            + $"GameObjectId=0x{gameObjectId:X} kind={kind} position={Format(position)} HP={currentHp}/{maxHp} "
            + $"InCombat={inCombat} Pulled={pulled} RegistryMatch={registryMatch} {registry}");
    }

    private static IGameObject? FindDalamudTarget(nint address)
    {
        var objects = Svc.Objects;
        for (var index = 0; index < objects.Length; index++)
        {
            if (objects[index] is { } candidate && candidate.Address == address)
            {
                return candidate;
            }
        }

        return null;
    }

    private static string Format(Vector3 position)
        => $"({position.X:F2}, {position.Y:F2}, {position.Z:F2})";
}
