using Dalamud.Game.Text;

namespace AutoHuntTrain.Core.Train;

// What a flag's text says about the instance: a number, "same" for the one the conductor already named, or nothing,
// and apart from that the instance the game wrote into the map link, which is only where the poster stood.
internal readonly record struct InstanceHint(int Number, bool Same, int Linked)
{
    public bool NamesNumber => Number > 0;
}

internal static class InstanceHints
{
    private const char FirstCircledDigit = '①';
    private const char LastCircledDigit = '⑥';
    private const char FirstLinkGlyph = (char)SeIconChar.Instance1;
    private const char LastLinkGlyph = (char)SeIconChar.Instance9;
    private const char ShortPrefix = 'i';
    private const string LongWord = "instance";
    private const string ShortWord = "inst";
    private const string SameWord = "same";
    private const string ToWord = "to";
    private const string SwitchWord = "switch";
    private const string SwapWord = "swap";
    private const string MoveWord = "move";
    private const string GoWord = "go";
    private const string CorrectWord = "correct";
    // A conductor's instance call is a short line of its own; a longer one is chatter that happens to hold a number.
    private const int MaxStandaloneWords = 6;

    // The first instance the text names counts. Directives are read only from a line of their own, so a flag's
    // "i1 pull, move 2 min" names instance 1.
    public static InstanceHint Parse(string text)
    {
        var span = text.AsSpan();
        var number = 0;
        var same = false;
        var linked = 0;
        for (var index = 0; index < span.Length; index++)
        {
            if (TryReadLinkGlyph(span[index], out var glyphInstance))
            {
                if (linked == 0)
                {
                    linked = glyphInstance;
                }

                continue;
            }

            if (number > 0)
            {
                continue;
            }

            if (TryReadNamed(span, index, out var named))
            {
                number = named;
                continue;
            }

            if (AtWordStart(span, index) && WholeWordEnd(span, index, SameWord) is not null)
            {
                same = true;
            }
        }

        return number > 0 ? new InstanceHint(number, false, linked) : new InstanceHint(0, same, linked);
    }

    // A line with no map link that is an instance call of its own: "i2", "move to instance 2", "swap to 2". A bare
    // number counts only right after a directive and at the end of a clause, so "move 2 min" calls nothing. A directive
    // outranks a plain mention and the later call wins, so "i1 done, swap to i2" calls instance 2.
    public static int ParseStandalone(string text)
    {
        var line = text.AsSpan().Trim();
        if (line.IsEmpty || CountWords(line) > MaxStandaloneWords)
        {
            return 0;
        }

        var named = 0;
        var directed = 0;
        for (var index = 0; index < line.Length; index++)
        {
            if (TryReadLinkGlyph(line[index], out var number) || TryReadNamed(line, index, out number))
            {
                named = number;
                continue;
            }

            if (TryReadDirected(line, index, out number))
            {
                directed = number;
            }
        }

        return directed > 0 ? directed : named;
    }

    // A circled digit anywhere, or "iN", "inst N" or "instance N" starting a word.
    private static bool TryReadNamed(ReadOnlySpan<char> text, int index, out int number)
    {
        var character = text[index];
        if (character is >= FirstCircledDigit and <= LastCircledDigit)
        {
            number = character - FirstCircledDigit + 1;
            return true;
        }

        number = 0;
        if (!AtWordStart(text, index) || char.ToLowerInvariant(character) != ShortPrefix)
        {
            return false;
        }

        var afterWord = WordEnd(text, index, LongWord) ?? WordEnd(text, index, ShortWord) ?? index + 1;
        return TryReadNumber(text, afterWord, out number, out _);
    }

    private static bool TryReadDirected(ReadOnlySpan<char> line, int index, out int number)
    {
        number = 0;
        if (!AtWordStart(line, index) || DirectiveEnd(line, index) is not { } directiveEnd)
        {
            return false;
        }

        var cursor = SkipWhitespace(line, directiveEnd);
        if (WholeWordEnd(line, cursor, ToWord) is { } toEnd)
        {
            cursor = SkipWhitespace(line, toEnd);
        }

        if (cursor >= line.Length)
        {
            return false;
        }

        if (TryReadLinkGlyph(line[cursor], out number) || TryReadNamed(line, cursor, out number))
        {
            return true;
        }

        return TryReadNumber(line, cursor, out number, out var numberEnd) && EndsClause(line, numberEnd);
    }

    private static int? DirectiveEnd(ReadOnlySpan<char> line, int index)
        => WholeWordEnd(line, index, SwitchWord)
        ?? WholeWordEnd(line, index, SwapWord)
        ?? WholeWordEnd(line, index, MoveWord)
        ?? WholeWordEnd(line, index, GoWord)
        ?? WholeWordEnd(line, index, CorrectWord);

    // Only the end of the line or punctuation may follow a bare number; "2 min" and "2:30" are no instance.
    private static bool EndsClause(ReadOnlySpan<char> line, int index)
    {
        var cursor = SkipWhitespace(line, index);
        if (cursor >= line.Length)
        {
            return true;
        }

        return char.IsPunctuation(line[cursor]) && (cursor + 1 >= line.Length || !char.IsAsciiDigit(line[cursor + 1]));
    }

    private static bool TryReadLinkGlyph(char character, out int instance)
    {
        if (character is >= FirstLinkGlyph and <= LastLinkGlyph)
        {
            instance = character - FirstLinkGlyph + 1;
            return true;
        }

        instance = 0;
        return false;
    }

    private static bool AtWordStart(ReadOnlySpan<char> text, int index)
        => index == 0 || !char.IsLetterOrDigit(text[index - 1]);

    private static int? WordEnd(ReadOnlySpan<char> text, int start, string word)
        => start <= text.Length && text[start..].StartsWith(word, StringComparison.OrdinalIgnoreCase) ? start + word.Length : null;

    private static int? WholeWordEnd(ReadOnlySpan<char> text, int start, string word)
        => WordEnd(text, start, word) is { } end && (end == text.Length || !char.IsLetterOrDigit(text[end])) ? end : null;

    private static int SkipWhitespace(ReadOnlySpan<char> text, int start)
    {
        var index = start;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return index;
    }

    // One digit, so "i 20" and "instance 12" name no instance.
    private static bool TryReadNumber(ReadOnlySpan<char> text, int start, out int number, out int end)
    {
        number = 0;
        var index = SkipWhitespace(text, start);
        end = index;
        if (index >= text.Length || text[index] is < '1' or > '9')
        {
            return false;
        }

        if (index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]))
        {
            return false;
        }

        number = text[index] - '0';
        end = index + 1;
        return true;
    }

    private static int CountWords(ReadOnlySpan<char> text)
    {
        var count = 0;
        var inWord = false;
        for (var index = 0; index < text.Length; index++)
        {
            var wordCharacter = char.IsLetterOrDigit(text[index]);
            if (wordCharacter && !inWord)
            {
                count++;
            }

            inWord = wordCharacter;
        }

        return count;
    }
}
