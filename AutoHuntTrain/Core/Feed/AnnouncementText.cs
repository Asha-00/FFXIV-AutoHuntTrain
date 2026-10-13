using System.Text;

namespace AutoHuntTrain.Core.Feed;

// What the relay's Discord text says beyond its fields: the start time in a timestamp tag, a clock time or a wait such
// as "in 10 minutes", the conductor's name after a "Conductor" label, and the emoji codes that only clutter a log line.
internal static partial class AnnouncementText
{
    private const string TimestampOpen = "<t:";
    private const char TagOpen = '<';
    private const char TagClose = '>';
    private const char Colon = ':';
    private const string ConductorWord = "Conductor";
    private const char WorldOpen = '[';
    private const char WorldClose = ']';
    private const char AnimatedEmojiMarker = 'a';
    // A Unix timestamp in seconds has ten digits until the year 2286.
    private const int MaxEpochDigits = 11;
    // A character name is two words of at most fifteen letters each.
    private const int NameWordMaxLength = 15;
    private const int EmojiNameMaxLength = 32;
    private const string PostedLabel = "Posted:";
    private const char LineBreak = '\n';
    private const int MaxHourDigits = 2;
    private const int MinuteDigits = 2;
    private const int MeridiemLength = 2;
    private const int HoursPerHalfDay = 12;
    private const int MinutesPerHour = 60;
    private const char EnDash = '\u2013';
    private const char EmDash = '\u2014';
    private const char Tilde = '~';

    // A start outside the game's lifetime is a mangled tag, not a train.
    private static readonly long EarliestEpoch = new DateTimeOffset(2013, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    private static readonly long LatestEpoch = new DateTimeOffset(2100, 1, 1, 0, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();
    // Two clock times further apart than this give the train's whole run, as in "07:45 PM - 08:30 PM", not a start window.
    private static readonly TimeSpan MaxClockWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan HandTypedClockBeforePost = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan HandTypedClockAfterPost = TimeSpan.FromMinutes(MaxRelativeMinutes);
    // A clock time written in one of these zones, server and Eorzea time among them, is not this PC's clock.
    private static readonly string[] timeZoneWords =
    [
        "UTC", "GMT", "ST", "ET", "server", "EST", "EDT", "CST", "CDT", "MST", "MDT", "PST", "PDT", "CT", "MT", "PT",
        "AKST", "AKDT", "HST", "BST", "CET", "CEST", "EET", "EEST", "WET", "WEST", "JST", "KST", "SGT", "AEST", "AEDT",
        "ACST", "ACDT", "AWST", "NZST", "NZDT",
    ];
    private static readonly string[] rangeWords = ["to", "or", "and"];

    // The first well-formed <t:EPOCH> or <t:EPOCH:STYLE> tag.
    public static bool TryReadStartTime(string text, out DateTime startAtUtc)
    {
        startAtUtc = default;
        var open = text.IndexOf(TimestampOpen, StringComparison.Ordinal);
        while (open >= 0)
        {
            var digitsStart = open + TimestampOpen.Length;
            var index = digitsStart;
            long epoch = 0;
            while (index < text.Length && char.IsAsciiDigit(text[index]) && index - digitsStart < MaxEpochDigits)
            {
                epoch = epoch * 10 + (text[index] - '0');
                index++;
            }

            var wellFormed = index > digitsStart && index < text.Length && (text[index] == TagClose || text[index] == Colon);
            if (wellFormed && epoch >= EarliestEpoch && epoch <= LatestEpoch)
            {
                startAtUtc = DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
                return true;
            }

            open = text.IndexOf(TimestampOpen, index, StringComparison.Ordinal);
        }

        return false;
    }

    // The first clock time such as "07:45 PM" outside the "Posted:" line, read on the day nearest the post: HuntAlerts
    // turns every Discord timestamp into exactly that before relaying the text, so the tag above is usually gone. A
    // second time after a range mark on the same line, as in "07:45 PM - 08:00 PM", makes a start window whose later
    // end is the start.
    public static bool TryReadClockTime(string text, DateTime postedAtUtc, out DateTime earliestAtUtc, out DateTime startAtUtc)
    {
        var lineStart = 0;
        while (lineStart < text.Length)
        {
            var lineEnd = LineEnd(text, lineStart);
            if (!StartsWithLabel(text, lineStart, lineEnd, PostedLabel) && TryFindClock(text, lineStart, lineEnd, postedAtUtc, out earliestAtUtc, out startAtUtc))
            {
                return true;
            }

            lineStart = lineEnd + 1;
        }

        earliestAtUtc = default;
        startAtUtc = default;
        return false;
    }

    private static int LineEnd(string text, int lineStart)
    {
        var lineEnd = text.IndexOf(LineBreak, lineStart);
        return lineEnd < 0 ? text.Length : lineEnd;
    }

    private static bool StartsWithLabel(string text, int start, int end, string label)
    {
        var index = start;
        while (index < end && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        return end - index >= label.Length && string.Compare(text, index, label, 0, label.Length, StringComparison.OrdinalIgnoreCase) == 0;
    }

    // The later time is the first itself when no window follows it. A reading in another zone is passed over whole, and
    // so is a hand-typed one that lands far from the post.
    private static bool TryFindClock(string text, int start, int end, DateTime postedAtUtc, out DateTime earliestAtUtc, out DateTime startAtUtc)
    {
        for (var index = start; index < end; index++)
        {
            if (index > start && char.IsLetterOrDigit(text[index - 1]))
            {
                continue;
            }

            if (!TryReadClockAt(text, index, end, out var first))
            {
                continue;
            }

            if (!TryReadClockAfterRangeMark(text, first.End, end, out var later))
            {
                later = first;
            }

            index = later.End - 1;
            if (FollowedByTimeZone(text, first.End, end) || FollowedByTimeZone(text, later.End, end))
            {
                continue;
            }

            earliestAtUtc = NearestLocalTime(first.Hour, first.Minute, postedAtUtc);
            startAtUtc = WindowEnd(later, earliestAtUtc);
            if ((first.HandTyped || later.HandTyped) && !NearPost(earliestAtUtc, postedAtUtc))
            {
                continue;
            }

            return true;
        }

        earliestAtUtc = default;
        startAtUtc = default;
        return false;
    }

    // A time typed by hand may be in the poster's own zone rather than this PC's, so it is believed only from a little
    // before the post to the longest wait a post gives ahead of its train; a zone or two away lands outside.
    private static bool NearPost(DateTime atUtc, DateTime postedAtUtc)
        => atUtc >= postedAtUtc - HandTypedClockBeforePost && atUtc <= postedAtUtc + HandTypedClockAfterPost;

    // "07:45 PM" as HuntAlerts writes it, or "7:45 PM" and "7:45pm" as typed by hand. No letter or digit may run on
    // after it, and the caller starts it on a word, so "17:45 PM" gives no time at all.
    private static bool TryReadClockAt(string text, int index, int end, out ClockTime clock)
    {
        clock = default;
        var cursor = index;
        if (!TryReadDigits(text, ref cursor, end, MaxHourDigits, out var twelveHour) || cursor >= end || text[cursor] != Colon)
        {
            return false;
        }

        var shortHour = cursor - index < MaxHourDigits;
        cursor++;
        var minuteStart = cursor;
        if (!TryReadDigits(text, ref cursor, end, MinuteDigits, out var minute) || cursor - minuteStart != MinuteDigits)
        {
            return false;
        }

        var spaced = cursor < end && text[cursor] == ' ';
        if (spaced)
        {
            cursor++;
        }

        if (end - cursor < MeridiemLength || char.ToUpperInvariant(text[cursor + 1]) != 'M')
        {
            return false;
        }

        var meridiem = char.ToUpperInvariant(text[cursor]);
        cursor += MeridiemLength;
        if ((meridiem != 'A' && meridiem != 'P') || (cursor < end && char.IsLetterOrDigit(text[cursor])))
        {
            return false;
        }

        if (twelveHour is < 1 or > HoursPerHalfDay || minute >= MinutesPerHour)
        {
            return false;
        }

        clock = new ClockTime(twelveHour % HoursPerHalfDay + (meridiem == 'P' ? HoursPerHalfDay : 0), minute, cursor, shortHour || !spaced);
        return true;
    }

    private static bool TryReadClockAfterRangeMark(string text, int start, int end, out ClockTime clock)
    {
        clock = default;
        var cursor = SkipSpacing(text, start, end);
        if (!TrySkipRangeMark(text, ref cursor, end))
        {
            return false;
        }

        cursor = SkipSpacing(text, cursor, end);
        return cursor < end && TryReadClockAt(text, cursor, end, out clock);
    }

    private static bool FollowedByTimeZone(string text, int start, int end)
    {
        var cursor = SkipSpacing(text, start, end);
        if (cursor < end && text[cursor] == '(')
        {
            cursor = SkipSpacing(text, cursor + 1, end);
        }

        var wordEnd = LettersEnd(text, cursor, end);
        return wordEnd > cursor && IsAnyWord(text.AsSpan(cursor, wordEnd - cursor), timeZoneWords);
    }

    // The later time of a window lands on the day nearest the earlier one; one no later, or further on than the widest
    // window, leaves the earlier time the start.
    private static DateTime WindowEnd(in ClockTime later, DateTime earliestAtUtc)
    {
        var candidate = NearestLocalTime(later.Hour, later.Minute, earliestAtUtc);
        var width = candidate - earliestAtUtc;
        return width > TimeSpan.Zero && width <= MaxClockWindow ? candidate : earliestAtUtc;
    }

    // A clock time carries no date; the train starts on whichever day puts it nearest the anchor, the post, so a train
    // posted at 23:50 for 00:10 lands on the next day.
    private static DateTime NearestLocalTime(int hour, int minute, DateTime anchorUtc)
    {
        var anchorDate = anchorUtc.ToLocalTime().Date;
        var best = anchorUtc;
        var bestGap = TimeSpan.MaxValue;
        for (var dayOffset = -1; dayOffset <= 1; dayOffset++)
        {
            var local = DateTime.SpecifyKind(anchorDate.AddDays(dayOffset).AddHours(hour).AddMinutes(minute), DateTimeKind.Local);
            var candidate = local.ToUniversalTime();
            var gap = (candidate - anchorUtc).Duration();
            if (gap >= bestGap)
            {
                continue;
            }

            bestGap = gap;
            best = candidate;
        }

        return best;
    }

    // Up to maxDigits digits with no further digit after them.
    private static bool TryReadDigits(string text, ref int cursor, int end, int maxDigits, out int value)
    {
        value = 0;
        var index = cursor;
        while (index < end && char.IsAsciiDigit(text[index]))
        {
            if (index - cursor == maxDigits)
            {
                return false;
            }

            value = value * 10 + (text[index] - '0');
            index++;
        }

        if (index == cursor)
        {
            return false;
        }

        cursor = index;
        return true;
    }

    // "-", an en or em dash, "~", "to", "or" or "and" between the two ends of a window.
    private static bool TrySkipRangeMark(string text, ref int cursor, int end)
    {
        if (cursor >= end)
        {
            return false;
        }

        if (text[cursor] is '-' or EnDash or EmDash or Tilde)
        {
            cursor++;
            return true;
        }

        var wordEnd = LettersEnd(text, cursor, end);
        if (wordEnd == cursor || !IsAnyWord(text.AsSpan(cursor, wordEnd - cursor), rangeWords))
        {
            return false;
        }

        cursor = wordEnd;
        return true;
    }

    // Spaces and the bold, italic and code marks around a value; "~" is left, since it may mark a window or an estimate.
    private static int SkipSpacing(string text, int start, int end)
    {
        var index = start;
        while (index < end && text[index] is ' ' or '\t' or '*' or '_' or '`')
        {
            index++;
        }

        return index;
    }

    private static int LettersEnd(string text, int start, int end)
    {
        var index = start;
        while (index < end && char.IsAsciiLetter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsAnyWord(ReadOnlySpan<char> word, string[] words)
    {
        for (var index = 0; index < words.Length; index++)
        {
            if (word.Equals(words[index], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    // "Conductor: First Last" or "Conductor: [World] First Last", with Discord marks and spaces allowed around the
    // label as in "**Conductor**: First Last"; the world is empty when the text named none.
    public static bool TryReadConductor(string text, out string name, out string worldName)
    {
        var label = text.IndexOf(ConductorWord, StringComparison.OrdinalIgnoreCase);
        while (label >= 0)
        {
            if (TryReadConductorAt(text, label, out name, out worldName))
            {
                return true;
            }

            label = text.IndexOf(ConductorWord, label + ConductorWord.Length, StringComparison.OrdinalIgnoreCase);
        }

        name = string.Empty;
        worldName = string.Empty;
        return false;
    }

    private static bool TryReadConductorAt(string text, int label, out string name, out string worldName)
    {
        name = string.Empty;
        worldName = string.Empty;
        if (label > 0 && char.IsLetterOrDigit(text[label - 1]))
        {
            return false;
        }

        var colon = SkipFiller(text, label + ConductorWord.Length);
        if (colon >= text.Length || text[colon] != Colon)
        {
            return false;
        }

        var index = SkipFiller(text, colon + 1);
        if (index < text.Length && text[index] == WorldOpen)
        {
            var close = text.IndexOf(WorldClose, index);
            if (close < 0)
            {
                return false;
            }

            worldName = text[(index + 1)..close].Trim();
            index = SkipFiller(text, close + 1);
        }

        var firstEnd = NameWordEnd(text, index);
        var secondStart = SkipFiller(text, firstEnd);
        var secondEnd = NameWordEnd(text, secondStart);
        var firstLength = firstEnd - index;
        var secondLength = secondEnd - secondStart;
        if (firstLength == 0 || secondLength == 0 || firstLength > NameWordMaxLength || secondLength > NameWordMaxLength)
        {
            worldName = string.Empty;
            return false;
        }

        name = string.Concat(text.AsSpan(index, firstLength), " ", text.AsSpan(secondStart, secondLength));
        return true;
    }

    // Drops <:name:id>, <a:name:id> and :name: codes; every other tag, the timestamp included, stays as it was.
    public static string StripEmoji(string text)
    {
        if (text.IndexOf(Colon) < 0)
        {
            return text.Trim();
        }

        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var character = text[index];
            if (character == TagOpen)
            {
                var close = text.IndexOf(TagClose, index);
                if (close < 0)
                {
                    builder.Append(text, index, text.Length - index);
                    break;
                }

                if (!IsCustomEmoji(text, index, close))
                {
                    builder.Append(text, index, close - index + 1);
                }

                index = close + 1;
                continue;
            }

            if (character == Colon && TryEmojiCodeEnd(text, index, out var codeEnd))
            {
                index = codeEnd + 1;
                continue;
            }

            builder.Append(character);
            index++;
        }

        return builder.ToString().Trim();
    }

    private static int SkipFiller(string text, int start)
    {
        var index = start;
        while (index < text.Length && (text[index] is ' ' or '	' || IsMarkdownMark(text[index])))
        {
            index++;
        }

        return index;
    }

    private static int NameWordEnd(string text, int start)
    {
        var index = start;
        while (index < text.Length && IsNameCharacter(text[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsNameCharacter(char character) => char.IsLetter(character) || character == '\'' || character == '-';

    private static bool IsMarkdownMark(char character) => character is '*' or '_' or '`' or '~' or '|';

    // <:name:id> or <a:name:id>, with the id all digits.
    private static bool IsCustomEmoji(string text, int open, int close)
    {
        var index = open + 1;
        if (index + 1 < close && char.ToLowerInvariant(text[index]) == AnimatedEmojiMarker && text[index + 1] == Colon)
        {
            index++;
        }

        if (index >= close || text[index] != Colon)
        {
            return false;
        }

        var nameStart = index + 1;
        var nameEnd = text.IndexOf(Colon, nameStart, close - nameStart);
        if (nameEnd <= nameStart || nameEnd + 1 >= close)
        {
            return false;
        }

        for (var digitIndex = nameEnd + 1; digitIndex < close; digitIndex++)
        {
            if (!char.IsAsciiDigit(text[digitIndex]))
            {
                return false;
            }
        }

        return true;
    }

    // :name: over letters, digits and underscores with at least one letter, so a clock time keeps its colons.
    private static bool TryEmojiCodeEnd(string text, int open, out int close)
    {
        close = -1;
        var hasLetter = false;
        var index = open + 1;
        while (index < text.Length && index - open <= EmojiNameMaxLength)
        {
            var character = text[index];
            if (character == Colon)
            {
                if (index == open + 1 || !hasLetter)
                {
                    return false;
                }

                close = index;
                return true;
            }

            if (char.IsAsciiLetter(character))
            {
                hasLetter = true;
            }
            else if (!char.IsAsciiDigit(character) && character != '_' && character != '+' && character != '-')
            {
                return false;
            }

            index++;
        }

        return false;
    }

    // A clock time on the 24-hour clock, with the index just past its text; HandTyped when it is not in HuntAlerts' own
    // "07:45 PM" form.
    private readonly record struct ClockTime(int Hour, int Minute, int End, bool HandTyped);
}
