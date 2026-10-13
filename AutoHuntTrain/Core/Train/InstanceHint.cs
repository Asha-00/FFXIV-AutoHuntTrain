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

    // The first instance the text names counts.
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
        return TryReadNumber(text, afterWord, out number);
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

    // One digit, so "i 20" and "instance 12" name no instance.
    private static bool TryReadNumber(ReadOnlySpan<char> text, int start, out int number)
    {
        number = 0;
        var index = start;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index >= text.Length || text[index] is < '1' or > '9')
        {
            return false;
        }

        if (index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]))
        {
            return false;
        }

        number = text[index] - '0';
        return true;
    }
}
