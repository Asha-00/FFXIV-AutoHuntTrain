namespace AutoHuntTrain;

public sealed partial class Configuration
{
    public const int FlagTeleportWaitSecondsMax = 600;

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

    // How a ride gets into the zone of a flag posted elsewhere: its own teleport, or someone else's first.
    public FlagTeleportMode FlagTeleportMode { get; set; } = FlagTeleportMode.TeleportMyself;

    // How long a ride waits for someone else's teleport before casting its own; at 0 it never does, and the leg gives
    // the flag up once the wait has lasted as long as a leg may travel.
    public int FlagTeleportWaitSeconds { get; set; } = 60;

    public bool AcceptPartyTeleports { get; set; } = false;
}
