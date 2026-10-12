namespace AutoHuntTrain.Core.Feed;

// The kind of hunt event represented by an announcement. Train remains the zero value so announcements created by
// the existing positional constructor continue to behave as trains unless a builder explicitly marks them otherwise.
internal enum AnnouncementKind : byte
{
    Train,
    SRank,
}
