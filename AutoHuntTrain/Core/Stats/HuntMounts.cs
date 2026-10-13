using AutoHuntTrain.Core.Feed;

namespace AutoHuntTrain.Core.Stats;

// A hunt mount is the reward of a meta achievement over two kill counters, one for rank A marks and one for rank S.
internal readonly record struct HuntMount(
    ExpansionGroup Group,
    ushort MountId,
    ushort AchievementId,
    ushort ARankAchievementId,
    ushort SRankAchievementId,
    ushort ARankNeeded,
    ushort SRankNeeded);

// The four mounts of the Hunt achievement category, in the order of the expansion groups trains are announced under.
// Counters are numbered across the table: counter 2n is mount n's rank A counter and 2n + 1 its rank S counter.
internal static class HuntMounts
{
    public const int Count = 4;
    public const int CounterCount = Count * 2;

    public static readonly HuntMount[] All =
    [
        new(ExpansionGroup.Centurio, 127, 1922, 1917, 1920, 3000, 2000),
        new(ExpansionGroup.Shadowbringers, 190, 2356, 2352, 2355, 2000, 1000),
        new(ExpansionGroup.Endwalker, 267, 3000, 2996, 2999, 2000, 1000),
        new(ExpansionGroup.Dawntrail, 351, 3537, 3533, 3536, 2000, 1000),
    ];

    public static int MountOf(int counterIndex) => counterIndex >> 1;

    public static bool IsSRank(int counterIndex) => (counterIndex & 1) == 1;

    public static int ARankCounter(int mountIndex) => mountIndex << 1;

    public static int SRankCounter(int mountIndex) => (mountIndex << 1) | 1;

    public static uint CounterAchievementId(int counterIndex)
    {
        var mount = All[MountOf(counterIndex)];
        return IsSRank(counterIndex) ? mount.SRankAchievementId : mount.ARankAchievementId;
    }

    public static uint CounterNeeded(int counterIndex)
    {
        var mount = All[MountOf(counterIndex)];
        return IsSRank(counterIndex) ? mount.SRankNeeded : mount.ARankNeeded;
    }
}
