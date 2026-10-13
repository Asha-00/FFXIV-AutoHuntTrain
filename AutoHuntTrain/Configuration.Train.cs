namespace AutoHuntTrain;

public sealed partial class Configuration
{
    public string ConductorName { get; set; } = string.Empty;

    public uint ConductorWorldId { get; set; }

    public bool ListenShout { get; set; } = true;

    public bool ListenYell { get; set; } = true;

    public bool ListenSay { get; set; } = true;

    public int LateJoinLimitSeconds { get; set; } = 120;

    // Counts only until the first flag is followed; QuietEndMinutes takes over once a mark is credited.
    public int IdleLimitMinutes { get; set; } = 15;

    public int QuietEndMinutes { get; set; } = 5;

    public bool EndOnConductorPhrase { get; set; } = true;

    public bool WaitForPull { get; set; } = true;

    public int PullWaitSeconds { get; set; } = 180;

    public bool EndWhenAllCredited { get; set; } = true;

    public bool TagWithRangedAttacks { get; set; } = false;
}
