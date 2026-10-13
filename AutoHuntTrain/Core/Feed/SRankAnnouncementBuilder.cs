using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// Validates HuntAlerts' typed S-rank relay and turns it into the same feed model used by train announcements. Relay
// fields are untrusted: an announcement is returned only when its world, expansion, destination and exact S-rank target
// all agree with game data.
internal static class SRankAnnouncementBuilder
{
    private const int MaxInstance = 9;

    public static bool TryBuild(
        in HuntAlertMessage message,
        int id,
        DateTime receivedAtUtc,
        out Announcement announcement,
        out string problem)
    {
        announcement = default;
        problem = string.Empty;

        if (!ExpansionGroups.TryParse(message.HuntKind ?? string.Empty, out var group))
        {
            problem = $"unknown hunt kind '{message.HuntKind}'";
            return false;
        }

        if (!Worlds.TryFindById(message.HuntWorldId, out var world))
        {
            problem = $"unknown world id {message.HuntWorldId}";
            return false;
        }

        if (message.creatureNameId is not > 0)
        {
            problem = "the relay names no creature";
            return false;
        }

        var suppliedCreatureId = message.creatureNameId.Value;
        ResolvePlace(message, out var aetheryteId, out var territoryId);
        if (territoryId == 0)
        {
            problem = "the relay names no known territory";
            return false;
        }

        if (!TryResolveCreature(message, suppliedCreatureId, territoryId, out var creatureNameId, out var mark, out problem))
        {
            return false;
        }

        var markIndex = HuntMarkRegistry.IndexOf(creatureNameId);
        if (!HuntMarkRegistry.IsExpansionWideAt(markIndex) && mark.TerritoryId != territoryId)
        {
            problem = $"{HuntMarkRegistry.NameOf(creatureNameId)} belongs to territory {mark.TerritoryId}, not announced territory {territoryId}";
            return false;
        }

        Vector2? coordinates = message.MapLocationCoords is { } point
            && float.IsFinite(point.X)
            && float.IsFinite(point.Y)
                ? new Vector2(point.X, point.Y)
                : null;
        if (coordinates is null)
        {
            RunLog.Info($"Feed: S rank {HuntMarkRegistry.NameOf(creatureNameId)} on {world.Name} names {TerritoryNames.Of(territoryId)} but no map coordinates; its live position will be resolved after arrival");
        }

        var instance = message.Instance is > 0 and <= MaxInstance ? message.Instance : 0;
        var postedAtUtc = PostedAt(message, receivedAtUtc);
        var text = AnnouncementText.StripEmoji(message.Message ?? string.Empty);

        announcement = new Announcement(
            id,
            receivedAtUtc,
            postedAtUtc,
            world,
            group,
            postedAtUtc,
            aetheryteId,
            territoryId,
            instance,
            coordinates,
            ConductorIdentity.None,
            text)
        {
            Kind = AnnouncementKind.SRank,
            CreatureNameId = creatureNameId,
        };

        return true;
    }

    private static bool TryResolveCreature(
        in HuntAlertMessage message,
        uint suppliedCreatureId,
        uint territoryId,
        out uint creatureNameId,
        out HuntMark mark,
        out string problem)
    {
        creatureNameId = suppliedCreatureId;
        problem = string.Empty;
        if (HuntMarkRegistry.TryGet(creatureNameId, out mark))
        {
            if (mark.Rank != HuntMarkRank.S)
            {
                problem = $"{HuntMarkRegistry.NameOf(creatureNameId)} is registered as rank {mark.Rank}, not S";
                return false;
            }

            return true;
        }

        var creatureName = ReadCreatureName(message.Message ?? string.Empty);
        if (creatureName.Length == 0)
        {
            problem = $"BNpcName {suppliedCreatureId} is not a registered hunt mark, and the relay text names no creature";
            mark = default;
            return false;
        }

        var matches = 0;
        var marks = HuntMarkRegistry.Marks;
        for (var index = 0; index < marks.Length; index++)
        {
            var candidate = marks[index];
            if (candidate.Rank != HuntMarkRank.S
                || !HuntMarkRegistry.NameAt(index).Equals(creatureName, StringComparison.CurrentCultureIgnoreCase)
                || (!HuntMarkRegistry.IsExpansionWideAt(index) && candidate.TerritoryId != territoryId))
            {
                continue;
            }

            matches++;
            creatureNameId = candidate.NameId;
            mark = candidate;
        }

        if (matches != 1)
        {
            problem = matches == 0
                ? $"BNpcName {suppliedCreatureId} is not registered, and no S rank named '{creatureName}' matches territory {territoryId}"
                : $"BNpcName {suppliedCreatureId} is not registered, and '{creatureName}' matches more than one S rank in territory {territoryId}";
            mark = default;
            return false;
        }

        RunLog.Info($"Feed: supplied creature id {suppliedCreatureId} is not a registered BNpcName; resolved '{creatureName}' to BNpcName {creatureNameId} in {TerritoryNames.Of(territoryId)}");
        return true;
    }

    private static string ReadCreatureName(string text)
    {
        const string label = "Creature:";
        var start = text.IndexOf(label, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
        {
            return string.Empty;
        }

        start += label.Length;
        while (start < text.Length && char.IsWhiteSpace(text[start]))
        {
            start++;
        }

        var end = start;
        while (end < text.Length && text[end] is not '\r' and not '\n')
        {
            end++;
        }

        return AnnouncementText.StripEmoji(text[start..end]).Trim();
    }

    private static DateTime PostedAt(in HuntAlertMessage message, DateTime fallbackUtc)
    {
        if (message.PostedEpoch > 0)
        {
            var epoch = message.PostedEpoch;
            if (epoch <= DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
            }
        }

        var posted = message.PostedTime.UtcDateTime;
        return posted.Year > 2000 ? posted : fallbackUtc;
    }

    // The aetheryte is the more specific destination fact. When the relay's territory disagrees with its aetheryte,
    // use the territory known by the client for that teleport target, matching the existing train intake behaviour.
    private static void ResolvePlace(in HuntAlertMessage message, out uint aetheryteId, out uint territoryId)
    {
        aetheryteId = message.StartingAetheryteId;
        territoryId = message.StartingTerritoryTypeId;

        if (aetheryteId != 0)
        {
            if (ZoneAetherytes.TryFindById(aetheryteId, out var aetheryteTerritoryId, out _))
            {
                if (territoryId != 0 && territoryId != aetheryteTerritoryId)
                {
                    RunLog.Debug($"Feed: S-rank territory {territoryId} disagrees with aetheryte {aetheryteId} in territory {aetheryteTerritoryId}; the aetheryte wins");
                }

                territoryId = aetheryteTerritoryId;
            }
            else
            {
                RunLog.Debug($"Feed: S-rank aetheryte {aetheryteId} is not a teleport target; the announcement keeps only its territory");
                aetheryteId = 0;
            }
        }

        if (territoryId != 0 && Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId) is null)
        {
            RunLog.Debug($"Feed: S-rank territory {territoryId} is not one this client knows; the announcement names no zone");
            territoryId = 0;
        }
    }
}
