namespace AutoHuntTrain.Core.Feed;

// Where a train's start time came from. Tag is the zero value so announcements built without one, the one-off S ranks
// among them, keep the start they were given; only PostTime marks a start the post never stated.
public enum StartSource : byte
{
    Tag,
    Clock,
    Relative,
    PostTime,
}
