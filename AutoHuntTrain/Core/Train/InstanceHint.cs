namespace AutoHuntTrain.Core.Train;

// What a flag's text says about the instance: a number, "same" for the one the conductor already named, or nothing.
internal readonly record struct InstanceHint(int Number, bool Same)
{
    public bool NamesNumber => Number > 0;
}

internal static class InstanceHints
{
    private const char FirstCircledDigit = '①';
    private const char LastCircledDigit = '⑥';
    private const char FirstGameInstanceGlyph = '\uE0B1';
    private const char LastGameInstanceGlyph = '\uE0B6';

    private enum HintSource
    {
        None = 0,
        MapGlyph = 100,
        ShortToken = 200,
        ExplicitInstance = 300,
        SwitchDirective = 400,
        CorrectionDirective = 500,
    }

    private readonly record struct Candidate(int Number, HintSource Source, int Position);

    // Scans the whole message instead of returning the first instance-looking token. The highest-confidence candidate
    // wins; when confidence is equal, the later candidate wins so appended conductor corrections take precedence.
    public static InstanceHint Parse(string text)
    {
        var best = default(Candidate);
        var same = false;

        for (var index = 0; index < text.Length; index++)
        {
            if (index == 0 || !char.IsLetterOrDigit(text[index - 1]))
            {
                if (TryReadDirective(text, index, "correct", HintSource.CorrectionDirective, out var correction))
                {
                    Choose(ref best, correction);
                }

                if (TryReadDirective(text, index, "switch", HintSource.SwitchDirective, out var switchHint)
                    || TryReadDirective(text, index, "swap", HintSource.SwitchDirective, out switchHint)
                    || TryReadDirective(text, index, "move", HintSource.SwitchDirective, out switchHint)
                    || TryReadDirective(text, index, "go", HintSource.SwitchDirective, out switchHint))
                {
                    Choose(ref best, switchHint);
                }

                if (TryReadExplicitInstance(text, index, out var explicitHint))
                {
                    Choose(ref best, explicitHint);
                }

                if (TryReadShortToken(text, index, out var shortHint))
                {
                    Choose(ref best, shortHint);
                }

                if (WordEnd(text, index, "same") is { } sameEnd
                    && (sameEnd == text.Length || !char.IsLetterOrDigit(text[sameEnd])))
                {
                    same = true;
                }
            }

            if (TryReadGlyph(text[index], out var glyphInstance))
            {
                Choose(ref best, new Candidate(glyphInstance, HintSource.MapGlyph, index));
            }
        }

        return best.Number > 0 ? new InstanceHint(best.Number, false) : new InstanceHint(0, same);
    }

    // Standalone chat is intentionally stricter than flag text. The whole line must be a concise instruction.
    public static int ParseStandalone(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return 0;
        }

        var best = Parse(trimmed);
        if (!best.NamesNumber)
        {
            return 0;
        }

        return IsConciseStandalone(trimmed) ? best.Number : 0;
    }

    private static bool TryReadDirective(string text, int start, string word, HintSource source, out Candidate candidate)
    {
        candidate = default;
        if (WordEnd(text, start, word) is not { } cursor
            || cursor < text.Length && char.IsLetterOrDigit(text[cursor]))
        {
            return false;
        }

        SkipSeparators(text, ref cursor);
        if (WordEnd(text, cursor, "to") is { } toEnd
            && (toEnd == text.Length || !char.IsLetterOrDigit(text[toEnd])))
        {
            cursor = toEnd;
            SkipSeparators(text, ref cursor);
        }

        ReadOptionalInstanceWord(text, ref cursor);
        SkipSeparators(text, ref cursor);
        if (cursor < text.Length && char.ToLowerInvariant(text[cursor]) == 'i'
            && (cursor + 1 == text.Length || !char.IsLetter(text[cursor + 1])))
        {
            cursor++;
        }

        if (!TryReadValue(text, cursor, out var number, out _))
        {
            return false;
        }

        candidate = new Candidate(number, source, start);
        return true;
    }

    private static bool TryReadExplicitInstance(string text, int start, out Candidate candidate)
    {
        candidate = default;
        var end = WordEnd(text, start, "instance") ?? WordEnd(text, start, "inst");
        if (end is null || end.Value < text.Length && char.IsLetter(text[end.Value]))
        {
            return false;
        }

        if (!TryReadValue(text, end.Value, out var number, out _))
        {
            return false;
        }

        candidate = new Candidate(number, HintSource.ExplicitInstance, start);
        return true;
    }

    private static bool TryReadShortToken(string text, int start, out Candidate candidate)
    {
        candidate = default;
        if (char.ToLowerInvariant(text[start]) != 'i'
            || start + 1 >= text.Length
            || !TryReadValue(text, start + 1, out var number, out _))
        {
            return false;
        }

        candidate = new Candidate(number, HintSource.ShortToken, start);
        return true;
    }

    private static bool TryReadValue(string text, int start, out int number, out int end)
    {
        var index = start;
        SkipSeparators(text, ref index);
        number = 0;
        end = index;
        if (index >= text.Length)
        {
            return false;
        }

        if (TryReadGlyph(text[index], out number))
        {
            end = index + 1;
            return true;
        }

        if (text[index] is < '1' or > '6'
            || index + 1 < text.Length && char.IsAsciiDigit(text[index + 1]))
        {
            return false;
        }

        number = text[index] - '0';
        end = index + 1;
        return true;
    }

    private static void Choose(ref Candidate best, in Candidate candidate)
    {
        if ((int)candidate.Source > (int)best.Source
            || candidate.Source == best.Source && candidate.Position >= best.Position)
        {
            best = candidate;
        }
    }

    private static void ReadOptionalInstanceWord(string text, ref int index)
    {
        if (WordEnd(text, index, "instance") is { } instanceEnd)
        {
            index = instanceEnd;
        }
        else if (WordEnd(text, index, "inst") is { } instEnd)
        {
            index = instEnd;
        }
    }

    private static bool IsConciseStandalone(string text)
    {
        var lowered = text.ToLowerInvariant();
        if (lowered.Contains("instance", StringComparison.Ordinal)
            || lowered.Contains("inst", StringComparison.Ordinal)
            || lowered.Contains("switch", StringComparison.Ordinal)
            || lowered.Contains("swap", StringComparison.Ordinal)
            || lowered.Contains("correct", StringComparison.Ordinal))
        {
            return CountWords(text) <= 5;
        }

        var trimmed = text.Trim();
        return trimmed.Length == 1 && TryReadGlyph(trimmed[0], out _)
            || trimmed.Length <= 4 && trimmed[0] is 'i' or 'I';
    }

    private static int CountWords(string text)
    {
        var count = 0;
        var inWord = false;
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                if (!inWord)
                {
                    count++;
                    inWord = true;
                }
            }
            else
            {
                inWord = false;
            }
        }

        return count;
    }

    private static void SkipSeparators(string text, ref int index)
    {
        while (index < text.Length
               && (char.IsWhiteSpace(text[index]) || text[index] is ':' or '-' or '=' or '>' or '[' or ']' or '(' or ')'))
        {
            index++;
        }
    }

    private static bool TryReadGlyph(char character, out int instance)
    {
        if (character is >= FirstCircledDigit and <= LastCircledDigit)
        {
            instance = character - FirstCircledDigit + 1;
            return true;
        }

        if (character is >= FirstGameInstanceGlyph and <= LastGameInstanceGlyph)
        {
            instance = character - FirstGameInstanceGlyph + 1;
            return true;
        }

        instance = 0;
        return false;
    }

    private static int? WordEnd(string text, int start, string word)
        => start + word.Length <= text.Length
           && string.Compare(text, start, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0
            ? start + word.Length
            : null;
}
