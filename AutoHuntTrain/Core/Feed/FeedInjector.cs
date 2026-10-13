using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AutoHuntTrain.Core.Feed;

// A made-up announcement pushed through the same intake as HuntAlerts, so the rules, details and automation can be
// exercised without a live relay. The original train syntax remains unchanged; "srank" selects the one-off S-rank form.
internal static class FeedInjector
{
    private const string Usage = AhtConstants.LogPrefix
        + " Usage: /aht inject <World> <DT|EW|SHB|Centurio> [<aetheryte>] [i<n>] [+<minutes>] [conductor:<First Last>] [body:<text>]"
        + " OR /aht inject srank <World> <creature name> <x> <y> [<aetheryte>] [i<n>] [body:<text>].";
    private const string ConductorPrefix = "conductor:";
    private const string BodyPrefix = "body:";
    private const string EscapedLineBreak = "\\n";
    private const string TrainType = "new_hunt";
    private const int DefaultLeadMinutes = 5;
    private const int MaxLeadMinutes = 360;
    private static readonly char[] wordSeparators = [' ', '\t'];

    public static void HandleCommand(string arguments, FeedListener feed)
    {
        var customBody = SplitBody(ref arguments);
        var words = arguments.Split(wordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 0 && string.Equals(words[0], FeedListener.RankSType, StringComparison.OrdinalIgnoreCase))
        {
            HandleSRank(words, customBody, feed);
            return;
        }

        HandleTrain(words, customBody, feed);
    }

    private static void HandleTrain(string[] words, string? customBody, FeedListener feed)
    {
        if (words.Length < 2)
        {
            Svc.Chat.PrintError(Usage);
            return;
        }

        if (!Worlds.TryFind(words[0], out var world))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No world matches '{words[0]}'.");
            return;
        }

        if (!ExpansionGroups.TryParse(words[1], out var group))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} '{words[1]}' is not an expansion group; use DT, EW, SHB or Centurio.");
            return;
        }

        var minutes = DefaultLeadMinutes;
        var leadGiven = false;
        var instance = 0;
        var conductor = string.Empty;
        var aetheryteName = new StringBuilder();
        for (var index = 2; index < words.Length; index++)
        {
            var word = words[index];
            if (word.StartsWith(ConductorPrefix, StringComparison.OrdinalIgnoreCase))
            {
                conductor = word[ConductorPrefix.Length..];
                if (index + 1 < words.Length)
                {
                    conductor = $"{conductor} {words[index + 1]}";
                    index++;
                }

                continue;
            }

            if (TryParseLead(word, out var lead))
            {
                minutes = lead;
                leadGiven = true;
                continue;
            }

            if (AutoGoto.TryParseInstance(word, out var parsedInstance))
            {
                instance = parsedInstance;
                continue;
            }

            AppendWord(aetheryteName, word);
        }

        var aetheryteId = 0u;
        var territoryId = 0u;
        var place = string.Empty;
        var header = string.Empty;
        if (aetheryteName.Length > 0)
        {
            var name = aetheryteName.ToString();
            if (!ZoneAetherytes.TryFindByName(name, out territoryId, out var aetheryte))
            {
                Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No single aetheryte matches '{name}'.");
                return;
            }

            aetheryteId = aetheryte.Id;
            place = $" at {TerritoryNames.Of(territoryId)} - {aetheryte.Name}";
            header = $"{Environment.NewLine}Start Zone: {TerritoryNames.Of(territoryId)}{Environment.NewLine}Aetheryte: {aetheryte.Name}";
        }

        var now = DateTimeOffset.UtcNow;
        var startAt = now.AddMinutes(minutes);
        var conductorText = conductor.Length > 0 ? $" (Conductor: [{world.Name}] {conductor})" : string.Empty;
        var newLine = Environment.NewLine;
        var body = customBody is null
            ? $"**[{world.Name}]** Hunt train starting {ClockText(startAt)}{place}{conductorText}."
            : CompleteBody(customBody, leadGiven ? startAt : null, now, conductor);
        var text = $"Kind: Hunt Train{newLine}Hunt: {ExpansionGroups.Name(group)}{header}{newLine}World: {world.Name}{newLine}Posted: {ClockText(now)}{newLine}{newLine}{body}";
        var currentWorldId = Worlds.TryCurrent(out var current) ? current.Id : 0u;
        var message = new HuntAlertMessage(text, TrainType, ExpansionGroups.Name(group), world.Id, currentWorldId, 0, 0, now, now.ToUnixTimeSeconds(), aetheryteId, territoryId, instance, null, null);
        var instanceText = instance > 0 ? $", instance {instance}" : string.Empty;
        var conductorNote = conductor.Length > 0 ? $", conductor {conductor}" : string.Empty;
        var startNote = customBody is null || leadGiven ? $"starting in {minutes} min" : "starting as its body says";
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Injected a {ExpansionGroups.Name(group)} train on {world.Name} {startNote}{place}{instanceText}{conductorNote}.");
        feed.Accept(message);
    }

    private static void HandleSRank(string[] words, string? customBody, FeedListener feed)
    {
        if (words.Length < 5)
        {
            Svc.Chat.PrintError(Usage);
            return;
        }

        if (!Worlds.TryFind(words[1], out var world))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No world matches '{words[1]}'.");
            return;
        }

        if (!TryFindCoordinatePair(words, 2, out var coordinateIndex, out var mapX, out var mapY))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Give the S rank's displayed map coordinates after its name, for example Gunitt 28.8 8.4.");
            return;
        }

        var creatureQuery = string.Join(' ', words[2..coordinateIndex]);
        if (!TryFindMarkByName(creatureQuery, out var creatureNameId, out var mark, out var creatureName))
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No single registered hunt mark matches '{creatureQuery}'.");
            return;
        }

        if (mark.Rank != HuntMarkRank.S)
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} {creatureName} is rank {mark.Rank}, not an S rank.");
            return;
        }

        var territoryId = mark.TerritoryId;
        if (territoryId == 0)
        {
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} {creatureName} has no single territory and cannot be injected with this helper.");
            return;
        }

        var group = ExpansionGroups.FromExpansionKind(mark.Expansion);

        var instance = 0;
        var aetheryteName = new StringBuilder();
        for (var index = coordinateIndex + 2; index < words.Length; index++)
        {
            if (AutoGoto.TryParseInstance(words[index], out var parsedInstance))
            {
                instance = parsedInstance;
                continue;
            }

            AppendWord(aetheryteName, words[index]);
        }

        var aetheryteId = 0u;
        var aetheryteText = string.Empty;
        if (aetheryteName.Length > 0)
        {
            var name = aetheryteName.ToString();
            if (!ZoneAetherytes.TryFindByName(name, out var aetheryteTerritoryId, out var aetheryte))
            {
                Svc.Chat.PrintError($"{AhtConstants.LogPrefix} No single aetheryte matches '{name}'.");
                return;
            }

            if (aetheryteTerritoryId != territoryId)
            {
                Svc.Chat.PrintError($"{AhtConstants.LogPrefix} {aetheryte.Name} is in {TerritoryNames.Of(aetheryteTerritoryId)}, not {TerritoryNames.Of(territoryId)} where {creatureName} appears.");
                return;
            }

            aetheryteId = aetheryte.Id;
            aetheryteText = $"{Environment.NewLine}Aetheryte: {aetheryte.Name}";
        }

        var now = DateTimeOffset.UtcNow;
        var coordinates = new Vector2(mapX, mapY);
        var zone = TerritoryNames.Of(territoryId);
        var body = customBody ?? $"**{creatureName}** reported on **{world.Name}** in {zone} at {TrainRelay.FormatCoordinates(coordinates)}.";
        var newLine = Environment.NewLine;
        var text = $"Kind: S Rank{newLine}Hunt: {ExpansionGroups.Name(group)}{newLine}Start Zone: {zone}{aetheryteText}{newLine}World: {world.Name}{newLine}Posted: {ClockText(now)}{newLine}{newLine}{body}";
        var currentWorldId = Worlds.TryCurrent(out var current) ? current.Id : 0u;
        var message = new HuntAlertMessage(text, FeedListener.RankSType, ExpansionGroups.Name(group), world.Id, currentWorldId, 0, 0, now, now.ToUnixTimeSeconds(), aetheryteId, territoryId, instance, coordinates, creatureNameId);
        var instanceText = instance > 0 ? $", instance {instance}" : string.Empty;
        var stopText = aetheryteId != 0 ? $", via {aetheryteName}" : string.Empty;
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Injected S rank {creatureName} on {world.Name} in {zone} at {TrainRelay.FormatCoordinates(coordinates)}{instanceText}{stopText}.");
        feed.Accept(message);
    }

    private static bool TryFindCoordinatePair(string[] words, int start, out int index, out float x, out float y)
    {
        for (index = start + 1; index < words.Length - 1; index++)
        {
            if (float.TryParse(words[index], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                && float.TryParse(words[index + 1], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                && float.IsFinite(x)
                && float.IsFinite(y))
            {
                return true;
            }
        }

        index = -1;
        x = 0f;
        y = 0f;
        return false;
    }

    private static bool TryFindMarkByName(string query, out uint nameId, out HuntMark mark, out string name)
    {
        nameId = 0;
        mark = default;
        name = string.Empty;
        var matches = 0;
        var marks = HuntMarkRegistry.Marks;
        for (var index = 0; index < marks.Length; index++)
        {
            var candidate = HuntMarkRegistry.NameAt(index);
            if (!candidate.Equals(query, StringComparison.CurrentCultureIgnoreCase)
                && !candidate.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }

            matches++;
            nameId = marks[index].NameId;
            mark = marks[index];
            name = candidate;
        }

        return matches == 1;
    }

    private static void AppendWord(StringBuilder builder, string word)
    {
        if (builder.Length > 0)
        {
            builder.Append(' ');
        }

        builder.Append(word);
    }

    private static string? SplitBody(ref string arguments)
    {
        var start = arguments.IndexOf(BodyPrefix, StringComparison.OrdinalIgnoreCase);
        while (start > 0 && !char.IsWhiteSpace(arguments[start - 1]))
        {
            start = arguments.IndexOf(BodyPrefix, start + BodyPrefix.Length, StringComparison.OrdinalIgnoreCase);
        }

        if (start < 0)
        {
            return null;
        }

        var body = arguments[(start + BodyPrefix.Length)..].Trim().Replace(EscapedLineBreak, Environment.NewLine, StringComparison.Ordinal);
        arguments = arguments[..start];
        return body;
    }

    // A body that gives no start of its own gets the +<minutes> one; without it the body goes as written, so the post
    // time stands in as it does for a real post that names none.
    private static string CompleteBody(string body, DateTimeOffset? startAt, DateTimeOffset now, string conductor)
    {
        var completed = body;
        if (startAt is { } start && !GivesStart(body, now.UtcDateTime))
        {
            completed = $"{completed}{Environment.NewLine}**Starting time**: {ClockText(start)}";
        }

        if (conductor.Length > 0 && !AnnouncementText.TryReadConductor(body, out _, out _))
        {
            completed = $"{completed}{Environment.NewLine}**Conductor**: {conductor}";
        }

        return completed;
    }

    private static bool GivesStart(string body, DateTime nowUtc)
        => AnnouncementText.TryReadStartTime(body, out _)
            || AnnouncementText.TryReadClockTime(body, nowUtc, out _, out _)
            || AnnouncementText.TryReadRelativeStart(body, out _, out _, out _);

    private static string ClockText(DateTimeOffset time)
        => time.ToLocalTime().ToString("hh:mm tt", CultureInfo.GetCultureInfo("en-US"));

    private static bool TryParseLead(string token, out int minutes)
    {
        minutes = 0;
        if (token.Length < 2 || (token[0] != '+' && token[0] != '-'))
        {
            return false;
        }

        if (!int.TryParse(token.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var magnitude) || magnitude > MaxLeadMinutes)
        {
            return false;
        }

        minutes = token[0] == '-' ? -magnitude : magnitude;
        return true;
    }
}
