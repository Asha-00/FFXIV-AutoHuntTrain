using ECommons.DalamudServices;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using System.Text;
using AddonSheet = Lumina.Excel.Sheets.Addon;

namespace AutoHuntTrain.Core.Game.Watchers;

// An Addon sheet prompt with its placeholders (a player's name, a place) cut out: the literal text before, between and
// after them, so a prompt is recognized in every client language. Layout-only macros (line break, non-breaking space,
// soft hyphen) are dropped from both sides so they cannot break the match.
internal readonly struct PromptTemplate
{
    public static readonly PromptTemplate Invalid = new([]);

    // One more than the placeholders: the text before the first, between each pair, and after the last.
    private readonly string[] literals;

    private PromptTemplate(string[] literals)
    {
        this.literals = literals;
    }

    public bool IsValid => literals is { Length: > 1 };

    public bool Matches(string prompt) => TryMatch(prompt, []);

    // Fills one range per placeholder with the text the prompt shows there. A placeholder is never empty, and the text
    // between two of them is taken at its first appearance, since neither a name nor a place contains it.
    public bool TryMatch(string prompt, Span<Range> placeholders)
    {
        if (!IsValid)
        {
            return false;
        }

        var head = literals[0];
        var tail = literals[^1];
        if (prompt.Length <= head.Length + tail.Length
            || !prompt.StartsWith(head, StringComparison.Ordinal)
            || !prompt.EndsWith(tail, StringComparison.Ordinal))
        {
            return false;
        }

        var end = prompt.Length - tail.Length;
        var start = head.Length;
        for (var literalIndex = 1; literalIndex < literals.Length - 1; literalIndex++)
        {
            var literal = literals[literalIndex];
            var searchFrom = start + 1;
            var found = searchFrom < end ? prompt.IndexOf(literal, searchFrom, end - searchFrom, StringComparison.Ordinal) : -1;
            if (found < 0)
            {
                return false;
            }

            Fill(placeholders, literalIndex - 1, start..found);
            start = found + literal.Length;
        }

        if (start >= end)
        {
            return false;
        }

        Fill(placeholders, literals.Length - 2, start..end);
        return true;
    }

    public override string ToString() => IsValid ? string.Join("<…>", literals) : "";

    // Macros next to each other with no text between them fill one placeholder.
    public static PromptTemplate FromAddonRow(uint rowId)
    {
        var row = Svc.Data.GetExcelSheet<AddonSheet>().GetRowOrDefault(rowId);
        if (row is null)
        {
            return Invalid;
        }

        var found = new List<string>();
        var current = new StringBuilder();
        var placeholderOpen = false;
        foreach (var payload in row.Value.Text)
        {
            if (payload.Type == ReadOnlySePayloadType.Text)
            {
                current.Append(Encoding.UTF8.GetString(payload.Body.Span));
                continue;
            }

            if (IsLayoutMacro(payload) || (placeholderOpen && current.Length == 0))
            {
                continue;
            }

            found.Add(Normalize(current.ToString()));
            current.Clear();
            placeholderOpen = true;
        }

        if (found.Count == 0)
        {
            return Invalid;
        }

        found.Add(Normalize(current.ToString()));
        return Usable(found) ? new PromptTemplate([.. found]) : Invalid;
    }

    public static string Normalize(string text)
    {
        var builder = new StringBuilder(text.Length);
        for (var charIndex = 0; charIndex < text.Length; charIndex++)
        {
            var character = text[charIndex];
            if (character is '\r' or '\n' or ' ' or '­')
            {
                continue;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    // Some literal text is needed to tell the prompt apart, and two placeholders with nothing between them cannot be split.
    private static bool Usable(List<string> found)
    {
        var length = 0;
        for (var literalIndex = 0; literalIndex < found.Count; literalIndex++)
        {
            var isBetween = literalIndex > 0 && literalIndex < found.Count - 1;
            if (isBetween && found[literalIndex].Length == 0)
            {
                return false;
            }

            length += found[literalIndex].Length;
        }

        return length > 0;
    }

    private static void Fill(Span<Range> placeholders, int index, Range range)
    {
        if (index < placeholders.Length)
        {
            placeholders[index] = range;
        }
    }

    private static bool IsLayoutMacro(in ReadOnlySePayload payload)
        => payload.Type == ReadOnlySePayloadType.Macro
        && payload.MacroCode is MacroCode.NewLine or MacroCode.NonBreakingSpace or MacroCode.SoftHyphen;
}
