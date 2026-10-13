using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using AutoHuntTrain.Core.Marks;
using System.Numerics;


namespace AutoHuntTrain.Core.Feed;

// One event the feed announced, with the destination information an automated hunt needs. Aetheryte, territory and
// instance 0 mean the relay could not say. Existing positional construction defaults to a train; other event kinds set
// Kind and their additional metadata through init properties.
internal readonly record struct Announcement(
    int Id,
    DateTime ReceivedAtUtc,
    DateTime PostedAtUtc,
    WorldInfo World,
    ExpansionGroup Group,
    DateTime StartAtUtc,
    uint AetheryteId,
    uint TerritoryId,
    int Instance,
    Vector2? MapCoordinates,
    ConductorIdentity Conductor,
    string Message)
{
    // Past this the conductor has left the start zone, so auto-join passes the train and a ride catches up instead.
    public static readonly TimeSpan InProgressAfter = TimeSpan.FromMinutes(3);

    // A train leaves the list this long after its start, and a ride still looking for it gives up then.
    public static readonly TimeSpan ListedAfterStart = TimeSpan.FromMinutes(20);

    // A post that names no start is usually written some minutes ahead of the train, so the post time standing in for
    // the start is given this long before the train counts as under way.
    public static readonly TimeSpan GuessedStartGrace = TimeSpan.FromMinutes(15);

    private readonly DateTime earliestStartAtUtc;

    // Train is the zero value so every existing Announcement constructor continues to create a train unless a builder
    // explicitly identifies another kind of hunt event.
    public AnnouncementKind Kind { get; init; }

    // HuntAlerts supplies the BNpcName row ID for one-off S-rank alerts. Trains do not name one fixed creature.
    public uint? CreatureNameId { get; init; }

    public StartSource StartSource { get; init; }

    // The earlier end when the post gave a window such as "in 10-15 minutes"; StartAtUtc holds the later end. Unset, or
    // past the start, it is the start itself.
    public DateTime EarliestStartAtUtc
    {
        get => earliestStartAtUtc == default || earliestStartAtUtc > StartAtUtc ? StartAtUtc : earliestStartAtUtc;
        init => earliestStartAtUtc = value;
    }

    public bool IsTrain => Kind == AnnouncementKind.Train;
    public bool IsSRank => Kind == AnnouncementKind.SRank;
    public bool StartIsGuessed => IsTrain && StartSource == StartSource.PostTime;
    public bool NamesStartWindow => EarliestStartAtUtc < StartAtUtc;
    public string CreatureNameOr(string fallback)
        => CreatureNameId is > 0
            ? HuntMarkRegistry.NameOf(CreatureNameId.Value)
            : fallback;
    public bool NamesConductor => Conductor.IsSet;
    public bool NamesAetheryte => AetheryteId != 0;
    public bool NamesTerritory => TerritoryId != 0;
    public bool NamesInstance => Instance > 0;
    public TimeSpan LeadAt(DateTime nowUtc) => StartAtUtc - nowUtc;

    // The latest the train is expected to set off: its start, or the end of the grace after a guessed one.
    public DateTime LatestStartAtUtc => StartIsGuessed ? StartAtUtc + GuessedStartGrace : StartAtUtc;
    public bool InProgressAt(DateTime nowUtc) => nowUtc - LatestStartAtUtc > InProgressAfter;

    // Only a start the post gave tells how far along its route the train has got; a ride on a train whose start was
    // guessed goes to the start zone instead.
    public bool CatchUpDueAt(DateTime nowUtc) => IsTrain && !StartIsGuessed && InProgressAt(nowUtc);

    public bool SameTrainAs(in Announcement other, TimeSpan tolerance)
        => IsTrain
            && other.IsTrain
            && other.World.Id == World.Id
            && other.Group == Group
            && (other.StartAtUtc - StartAtUtc).Duration() <= tolerance;

    // A post with no start is often followed by one that gives it, such as "starting in 10 minutes"; that post is the
    // same train when it is for the same world and group and was written within the window. A start before the first
    // post can only be a misread time.
    public bool StartGivenBy(in Announcement later, TimeSpan window)
        => StartIsGuessed
            && later.IsTrain
            && !later.StartIsGuessed
            && later.World.Id == World.Id
            && later.Group == Group
            && (later.PostedAtUtc - PostedAtUtc).Duration() <= window
            && later.StartAtUtc >= StartAtUtc;
}
