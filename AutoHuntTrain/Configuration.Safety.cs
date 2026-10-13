namespace AutoHuntTrain;

public sealed partial class Configuration
{
    public bool AutoRepair { get; set; } = false;
    public int AutoRepairThresholdPercent { get; set; } = 20;
    public RepairMode RepairMode { get; set; } = RepairMode.SelfThenNpc;
    // Null sends NPC repairs to the Grand Company mender.
    public RepairNpc? PreferredRepairNpc { get; set; }

    public bool AutoConsume { get; set; } = false;
    // 0 re-eats only once the buff has fully worn off.
    public int AutoConsumeMinMinutes { get; set; } = 3;
    public List<ConsumableEntry> AutoConsumeItems { get; set; } = [];

    // A conductor posts the next flag within seconds of a kill, so a lull shorter than this is not one.
    public const int UpkeepQuietSecondsMin = 20;

    // How long a ride waits with no new flag before it repairs or eats.
    public int UpkeepQuietSeconds { get; set; } = UpkeepQuietSecondsMin;

    public bool AcceptPartyInvites { get; set; } = true;
    public bool PostLookingForGroup { get; set; } = false;
    public string LookingForGroupText { get; set; } = "LFG";
    public bool CreatePartyFinderListing { get; set; } = false;
    // Cut to PartyFinderListing.CommentMaxBytes when the listing is put up.
    public string PartyFinderComment { get; set; } = "Hunt train - join for credit";
    public bool LeavePartyAfterRide { get; set; } = true;

    public bool GmAlertStopRun { get; set; } = true;
    public bool GmAlertToast { get; set; } = false;
    public bool GmAlertChat { get; set; } = false;
    public bool GmAlertSound { get; set; } = false;
    public int GmAlertBeepCount { get; set; } = 3;
    public int GmAlertBeepDurationMs { get; set; } = 250;
    public int GmAlertBeepFrequencyHz { get; set; } = 900;
    public bool GmAlertKillGame { get; set; } = false;
    public List<string> GmAlertCommands { get; set; } = [];
}
