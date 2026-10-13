using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Marks;

namespace AutoHuntTrain.Core.Stats;

// Fields added after the first release default when an older history file lacks them, and null reads as unknown.
[Serializable]
public sealed class RunRecord
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public double DurationSeconds { get; set; }

    public string WorldName { get; set; } = "";
    public string DataCenterName { get; set; } = "";
    public ExpansionKind? Expansion { get; set; }
    public ExpansionGroup? Group { get; set; }
    // Older history files lack this field and therefore deserialize it as false, preserving their train presentation.
    public bool IsSRank { get; set; }
    public bool CrossedDataCenter { get; set; }
    public RideOutcome? Outcome { get; set; }

    public int MarksCredited { get; set; }
    public int AlliedSeals { get; set; }
    public int CenturioSeals { get; set; }
    public int Nuts { get; set; }

    public string JobAbbreviation { get; set; } = "";

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public int Seals => AlliedSeals + CenturioSeals;
    public double MarksPerHour => DurationSeconds > 0 ? MarksCredited / (DurationSeconds / 3600.0) : 0;
    public bool Finished => Outcome is RideOutcome.AllCredited or RideOutcome.ConductorQuiet;

    // A record from before groups were kept still names one through its expansion; Centurio's three read alike.
    public ExpansionGroup? ResolveGroup() => Group ?? (Expansion is { } kind ? ExpansionGroups.FromExpansionKind(kind) : null);
}
