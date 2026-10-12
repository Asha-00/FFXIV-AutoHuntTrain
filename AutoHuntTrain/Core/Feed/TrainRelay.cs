using AutoHuntTrain.Core.Game;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Marks;
using ECommons.Automation;
using ECommons.Throttlers;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AutoHuntTrain.Core.Feed;

// Posts a one-line summary of a train or S-rank relay to a chat channel. A train reads, for example,
// "Dawntrail train on Siren! Kozama'uka (9.0, 12.0) i2 - Ok'Hanu". An S rank uses its exact creature name instead of
// an expansion train label. <flag> replaces the coordinates when the map flag was placed on the announced spot first.
internal static class TrainRelay
{
    // The game refuses a chat line longer than this many UTF-8 bytes.
    private const int MaxMessageBytes = 500;
    private const int ThrottleMs = 1_000;
    private const string ThrottleKey = "aht_train_relay";
    private const string FlagPlaceholder = "<flag>";
    private const string AetheryteSeparator = " - ";
    private const string CoordinateFormat = "0.0";

    private static readonly string[] commands =
    [
        "/s", "/y", "/sh", "/p", "/a", "/fc",
        "/l1", "/l2", "/l3", "/l4", "/l5", "/l6", "/l7", "/l8",
        "/cwl1", "/cwl2", "/cwl3", "/cwl4", "/cwl5", "/cwl6", "/cwl7", "/cwl8",
        "/echo",
    ];

    public static int ChannelCount => commands.Length;

    public static bool Send(in Announcement announcement, RelayChannel channel, bool withFlag)
    {
        if (!EzThrottler.Throttle(ThrottleKey, ThrottleMs))
        {
            return false;
        }

        if (announcement.IsSRank && announcement.CreatureNameId is not > 0)
        {
            RunLog.Warning($"Relay: the S-rank announcement on {announcement.World.Name} names no creature and was not posted");
            return false;
        }

        var flagged = withFlag
            && TrainFacts.TryFlagPoint(announcement, out var point)
            && MapFlag.TrySetFlag(announcement.TerritoryId, point);
        var command = Command(channel);
        var line = Fit(Chat.SanitiseText(string.Concat(command, " ", BuildText(announcement, flagged))));
        try
        {
            Chat.SendMessage(line);
            var subject = announcement.IsSRank
                ? $"S rank {HuntMarkRegistry.NameOf(announcement.CreatureNameId!.Value)}"
                : $"{ExpansionGroups.Name(announcement.Group)} train";
            RunLog.Info($"Relay: posted {subject} on {announcement.World.Name} to {command}{(flagged ? " with the map flag" : string.Empty)}");
            return true;
        }
        catch (Exception exception)
        {
            var kind = announcement.IsSRank ? "S rank" : "train";
            RunLog.Warning(exception, $"Relay: posting the {kind} on {announcement.World.Name} to {command} failed");
            return false;
        }
    }

    public static string BuildText(in Announcement announcement, bool flagged)
    {
        var heading = announcement.IsSRank
            ? SRankHeading(announcement)
            : Loc.T(L.Details.RelayText, ExpansionGroups.LocalName(announcement.Group), announcement.World.Name);
        var builder = new StringBuilder(heading);
        var zone = TrainFacts.StartZone(announcement);
        var coordinates = flagged
            ? FlagPlaceholder
            : TrainFacts.TryFlagPoint(announcement, out var point)
                ? FormatCoordinates(point)
                : string.Empty;
        if (zone.Length > 0 || coordinates.Length > 0)
        {
            builder.Append(' ').Append(zone);
            if (zone.Length > 0 && coordinates.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append(coordinates);
        }

        if (announcement.NamesInstance)
        {
            builder.Append(' ').Append(Loc.T(L.Details.InstanceShort, announcement.Instance));
        }

        var aetheryte = TrainFacts.Aetheryte(announcement);
        if (aetheryte.Length > 0)
        {
            builder.Append(AetheryteSeparator).Append(aetheryte);
        }

        return builder.ToString();
    }

    public static string FormatCoordinates(Vector2 coordinates)
        => string.Concat(
            "(",
            coordinates.X.ToString(CoordinateFormat, CultureInfo.InvariantCulture),
            ", ",
            coordinates.Y.ToString(CoordinateFormat, CultureInfo.InvariantCulture),
            ")");

    private static string SRankHeading(in Announcement announcement)
    {
        if (announcement.CreatureNameId is not > 0)
        {
            return Loc.T(L.Details.RelayTextSRank, Loc.T(L.Details.NotNamed), announcement.World.Name);
        }

        return Loc.T(
            L.Details.RelayTextSRank,
            HuntMarkRegistry.NameOf(announcement.CreatureNameId.Value),
            announcement.World.Name);
    }

    private static string Command(RelayChannel channel)
        => (uint)channel < (uint)commands.Length
            ? commands[(int)channel]
            : commands[(int)RelayChannel.Party];

    private static string Fit(string line)
    {
        var length = line.Length;
        while (length > 0 && Encoding.UTF8.GetByteCount(line.AsSpan(0, length)) > MaxMessageBytes)
        {
            length--;
        }

        if (length < line.Length && length > 0 && char.IsHighSurrogate(line[length - 1]))
        {
            length--;
        }

        return length == line.Length ? line : line[..length];
    }
}
