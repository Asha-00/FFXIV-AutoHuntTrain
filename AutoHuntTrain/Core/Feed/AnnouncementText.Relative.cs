namespace AutoHuntTrain.Core.Feed;

// A start given as a wait after the post rather than a time: "in 10 minutes", "10-15 min", "starting in about 5
// minutes", "in 10 to 15 minutes", "~15m", "in half an hour", or none at all in "starting now". An amount alone is not
// enough, since posts also give the pace and the break between zones; it needs a cue right before it or right after it.
internal static partial class AnnouncementText
{
    // Two hours covers any post written ahead of its train; a longer wait is a note about something else.
    private const int MaxRelativeMinutes = 120;
    private const int MaxAmountDigits = 3;
    private const int MaxFillerWords = 2;
    private const string FromWord = "from";
    private const string NowWord = "now";
    private const string ToWord = "to";
    private const string GoWord = "go";
    private const string RightWord = "right";

    private static readonly string[] minuteUnits = ["m", "min", "mins", "minute", "minutes"];
    private static readonly string[] hourUnits = ["h", "hr", "hrs", "hour", "hours"];
    private static readonly string[] cueWords =
    [
        "in", "within", "start", "starts", "starting", "begin", "begins", "beginning", "depart", "departs", "departing",
        "departure", "leave", "leaves", "leaving", "eta", "soon", "between",
    ];
    // May stand between the cue and the amount, as in "in about 10 minutes" or "within the next 10 minutes". "About" on
    // its own is no cue, since "takes about 20 minutes" gives a length.
    private static readonly string[] fillerWords = ["about", "around", "approx", "approximately", "roughly", "like", "maybe", "just", "only", "another", "the", "next"];
    // Right after the amount these make it a pace, an age or a length, as in "5 min per zone", "10 min ago", "2 min
    // between pulls" or "~45 min run".
    private static readonly string[] vetoWords =
    [
        "per", "each", "every", "a", "an", "ago", "late", "later", "early", "earlier", "behind", "long", "break", "breaks",
        "after", "before", "past", "apart", "of", "between", "run", "runs", "total",
    ];
    private static readonly string[] lengthWords = ["for", "take", "takes", "taking", "last", "lasts", "lasting", "duration", "length"];
    // After a bare "m" these make it metres, as in "~50m north".
    private static readonly string[] distanceWords = ["away", "north", "south", "east", "west"];
    private static readonly string[] untilWords = ["until", "till", "til"];
    // Right before "now" these say the train sets off with the post.
    private static readonly string[] setOffWords =
    [
        "start", "starts", "starting", "begin", "begins", "beginning", "depart", "departs", "departing", "leave", "leaves",
        "leaving",
    ];
    // Longest first, so "half an hour" is not read as "an hour".
    private static readonly SpokenAmount[] spokenAmounts =
    [
        new("an hour and a half", 90),
        new("half an hour", 30),
        new("a half hour", 30),
        new("half hour", 30),
        new("an hour", 60),
    ];

    // The first wait outside the "Posted:" line, in minutes after the post; a window such as "10-15 min" gives both its
    // ends, and the phrase is where the text says it.
    public static bool TryReadRelativeStart(string text, out int earliestMinutes, out int latestMinutes, out Range phrase)
    {
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var lineEnd = LineEnd(text, lineStart);
            if (!StartsWithLabel(text, lineStart, lineEnd, PostedLabel) && TryFindWait(text, lineStart, lineEnd, out var wait))
            {
                earliestMinutes = wait.EarliestMinutes;
                latestMinutes = wait.LatestMinutes;
                phrase = wait.Start..wait.End;
                return true;
            }

            lineStart = lineEnd + 1;
        }

        earliestMinutes = 0;
        latestMinutes = 0;
        phrase = default;
        return false;
    }

    private static bool TryFindWait(string text, int start, int end, out Wait wait)
    {
        for (var index = start; index < end; index++)
        {
            if (!StartsAmount(text, start, index))
            {
                continue;
            }

            if (TryReadSetOffNow(text, start, index, end, out wait))
            {
                return true;
            }

            if (!TryReadAmount(text, index, end, out wait))
            {
                continue;
            }

            var following = ClassifyFollowing(text, wait, end);
            if (following == Following.Veto || (following != Following.Cue && !HasLeadingCue(text, start, index)))
            {
                continue;
            }

            return true;
        }

        wait = default;
        return false;
    }

    // An amount starts a word, and not inside a number such as the "5" of "1.5" or the "30" of "7:30".
    private static bool StartsAmount(string text, int lineStart, int index)
    {
        if (index == lineStart)
        {
            return true;
        }

        var previous = text[index - 1];
        if (char.IsLetterOrDigit(previous))
        {
            return false;
        }

        return previous is not ('.' or ',' or Colon) || index - 1 == lineStart || !char.IsAsciiDigit(text[index - 2]);
    }

    private static bool TryReadAmount(string text, int index, int end, out Wait wait)
        => char.IsAsciiDigit(text[index])
            ? TryReadCountedAmount(text, index, end, out wait)
            : TryReadSpokenAmount(text, index, end, out wait);

    // "10 min", "10-15 minutes", "10 to 15 min", "1h" or "1-2 hours". A fraction is never read, so "1.5 hours" gives no
    // wait rather than the wrong one.
    private static bool TryReadCountedAmount(string text, int index, int end, out Wait wait)
    {
        wait = default;
        var cursor = index;
        if (!TryReadDigits(text, ref cursor, end, MaxAmountDigits, out var first) || FollowedByFraction(text, cursor, end))
        {
            return false;
        }

        var second = first;
        var rangeCursor = SkipSpacing(text, cursor, end);
        if (TrySkipRangeMark(text, ref rangeCursor, end))
        {
            rangeCursor = SkipSpacing(text, rangeCursor, end);
            if (TryReadDigits(text, ref rangeCursor, end, MaxAmountDigits, out var upper) && !FollowedByFraction(text, rangeCursor, end))
            {
                second = upper;
                cursor = rangeCursor;
            }
        }

        var unitStart = SkipSpacing(text, cursor, end);
        var unitEnd = LettersEnd(text, unitStart, end);
        if (unitEnd == unitStart || (unitEnd < end && char.IsAsciiDigit(text[unitEnd])))
        {
            return false;
        }

        var unit = text.AsSpan(unitStart, unitEnd - unitStart);
        var minutesPerUnit = IsAnyWord(unit, minuteUnits) ? 1 : IsAnyWord(unit, hourUnits) ? MinutesPerHour : 0;
        var latest = Math.Max(first, second) * minutesPerUnit;
        if (minutesPerUnit == 0 || latest is < 1 or > MaxRelativeMinutes)
        {
            return false;
        }

        wait = new Wait(Math.Min(first, second) * minutesPerUnit, latest, index, unitEnd, unit.Length == 1 && minutesPerUnit == 1);
        return true;
    }

    // "starting now" or "leaving right now": the post was written as the train set off, so there is no wait, and the
    // start is the post time as given rather than a guess.
    private static bool TryReadSetOffNow(string text, int lineStart, int index, int end, out Wait wait)
    {
        wait = default;
        var nowEnd = index + NowWord.Length;
        if (nowEnd > end
            || string.Compare(text, index, NowWord, 0, NowWord.Length, StringComparison.OrdinalIgnoreCase) != 0
            || (nowEnd < end && char.IsLetterOrDigit(text[nowEnd])))
        {
            return false;
        }

        var wordEnd = PreviousWordEnd(text, lineStart, index);
        var wordStart = PreviousWordStart(text, lineStart, wordEnd);
        if (text.AsSpan(wordStart, wordEnd - wordStart).Equals(RightWord, StringComparison.OrdinalIgnoreCase))
        {
            wordEnd = PreviousWordEnd(text, lineStart, wordStart);
            wordStart = PreviousWordStart(text, lineStart, wordEnd);
        }

        if (wordEnd == wordStart || !IsAnyWord(text.AsSpan(wordStart, wordEnd - wordStart), setOffWords))
        {
            return false;
        }

        wait = new Wait(0, 0, wordStart, nowEnd, InBareMinutes: false);
        return true;
    }

    // Back over the spaces and marks before the index, to just past the word they follow.
    private static int PreviousWordEnd(string text, int lineStart, int index)
    {
        var cursor = index;
        while (cursor > lineStart && text[cursor - 1] is ' ' or '\t' or '*' or '_' or '`')
        {
            cursor--;
        }

        return cursor;
    }

    private static int PreviousWordStart(string text, int lineStart, int wordEnd)
    {
        var cursor = wordEnd;
        while (cursor > lineStart && char.IsAsciiLetter(text[cursor - 1]))
        {
            cursor--;
        }

        return cursor;
    }

    private static bool FollowedByFraction(string text, int index, int end)
        => index + 1 < end && text[index] is '.' or ',' or Colon && char.IsAsciiDigit(text[index + 1]);

    private static bool TryReadSpokenAmount(string text, int index, int end, out Wait wait)
    {
        wait = default;
        if (!char.IsAsciiLetter(text[index]))
        {
            return false;
        }

        for (var amountIndex = 0; amountIndex < spokenAmounts.Length; amountIndex++)
        {
            var spoken = spokenAmounts[amountIndex];
            var phraseEnd = index + spoken.Phrase.Length;
            if (phraseEnd > end || string.Compare(text, index, spoken.Phrase, 0, spoken.Phrase.Length, StringComparison.OrdinalIgnoreCase) != 0)
            {
                continue;
            }

            if (phraseEnd < end && char.IsLetterOrDigit(text[phraseEnd]))
            {
                continue;
            }

            wait = new Wait(spoken.Minutes, spoken.Minutes, index, phraseEnd, InBareMinutes: false);
            return true;
        }

        return false;
    }

    // "from now", "until" or "to go" right after the amount is a wait for the start; a pace, an age, a length or, after a
    // bare "m", a distance rules it out.
    private static Following ClassifyFollowing(string text, in Wait wait, int end)
    {
        var cursor = SkipSpacing(text, wait.End, end);
        if (cursor < end && text[cursor] == '/')
        {
            return Following.Veto;
        }

        var wordEnd = LettersEnd(text, cursor, end);
        if (wordEnd == cursor)
        {
            return Following.Neutral;
        }

        var word = text.AsSpan(cursor, wordEnd - cursor);
        if (IsAnyWord(word, vetoWords) || (wait.InBareMinutes && IsAnyWord(word, distanceWords)))
        {
            return Following.Veto;
        }

        if (IsAnyWord(word, untilWords))
        {
            return Following.Cue;
        }

        if (word.Equals(FromWord, StringComparison.OrdinalIgnoreCase))
        {
            return NextWordIs(text, wordEnd, end, NowWord) ? Following.Cue : Following.Veto;
        }

        return word.Equals(ToWord, StringComparison.OrdinalIgnoreCase) && NextWordIs(text, wordEnd, end, GoWord) ? Following.Cue : Following.Neutral;
    }

    private static bool NextWordIs(string text, int start, int end, string expected)
    {
        var cursor = SkipSpacing(text, start, end);
        var wordEnd = LettersEnd(text, cursor, end);
        return wordEnd > cursor && text.AsSpan(cursor, wordEnd - cursor).Equals(expected, StringComparison.OrdinalIgnoreCase);
    }

    // A cue word right before the amount, with at most a couple of filler words between, or a "~" as in "~15m" unless
    // it follows a length word, as in "runs for ~45 min", where it only stands for "about".
    private static bool HasLeadingCue(string text, int lineStart, int amountStart)
    {
        var cursor = amountStart - 1;
        for (var wordCount = 0; wordCount <= MaxFillerWords; wordCount++)
        {
            while (cursor >= lineStart && !char.IsLetterOrDigit(text[cursor]))
            {
                if (IsEstimateMark(text, cursor, lineStart))
                {
                    return !FollowsLengthWord(text, lineStart, cursor);
                }

                cursor--;
            }

            if (cursor < lineStart || !char.IsAsciiLetter(text[cursor]))
            {
                return false;
            }

            var wordEnd = cursor + 1;
            while (cursor >= lineStart && char.IsAsciiLetter(text[cursor]))
            {
                cursor--;
            }

            var word = text.AsSpan(cursor + 1, wordEnd - cursor - 1);
            if (IsAnyWord(word, cueWords))
            {
                return true;
            }

            if (!IsAnyWord(word, fillerWords))
            {
                return false;
            }
        }

        return false;
    }

    // A doubled "~~" is Discord's strike-through, and one after a number joins the ends of a window as in "10~15".
    private static bool IsEstimateMark(string text, int index, int lineStart)
    {
        if (text[index] != Tilde || (index + 1 < text.Length && text[index + 1] == Tilde))
        {
            return false;
        }

        var before = index - 1;
        while (before >= lineStart && text[before] == ' ')
        {
            before--;
        }

        return before < lineStart || (text[before] != Tilde && !char.IsAsciiDigit(text[before]));
    }

    private static bool FollowsLengthWord(string text, int lineStart, int markIndex)
    {
        var wordEnd = PreviousWordEnd(text, lineStart, markIndex);
        while (wordEnd > lineStart && text[wordEnd - 1] == Colon)
        {
            wordEnd = PreviousWordEnd(text, lineStart, wordEnd - 1);
        }

        var wordStart = PreviousWordStart(text, lineStart, wordEnd);
        return wordEnd > wordStart && IsAnyWord(text.AsSpan(wordStart, wordEnd - wordStart), lengthWords);
    }

    private enum Following : byte
    {
        Neutral,
        Cue,
        Veto,
    }

    private readonly record struct SpokenAmount(string Phrase, int Minutes);

    // Minutes after the post, the text it was read from, and whether its unit was a bare "m" that could be metres.
    private readonly record struct Wait(int EarliestMinutes, int LatestMinutes, int Start, int End, bool InBareMinutes);
}
