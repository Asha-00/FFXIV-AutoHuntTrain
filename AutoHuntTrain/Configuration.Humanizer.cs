namespace AutoHuntTrain;

public enum HumanAction : byte
{
    MoveOff,
    Teleport,
    Engage,
    AcceptInvite,
    LookingForGroup,
    PartyFinder,
}

public readonly record struct DelayRange(float MinSeconds, float MaxSeconds);

public sealed partial class Configuration
{
    public bool HumanizerEnabled { get; set; } = true;

    public float MoveOffDelayMinSeconds { get; set; } = 1.0f;

    public float MoveOffDelayMaxSeconds { get; set; } = 3.5f;

    public float TeleportDelayMinSeconds { get; set; } = 0.3f;

    public float TeleportDelayMaxSeconds { get; set; } = 1.5f;

    public float EngageDelayMinSeconds { get; set; } = 0.5f;

    public float EngageDelayMaxSeconds { get; set; } = 2.0f;

    public float InviteDelayMinSeconds { get; set; } = 1.0f;

    public float InviteDelayMaxSeconds { get; set; } = 3.0f;

    public float LookingForGroupDelayMinSeconds { get; set; } = 2.0f;

    public float LookingForGroupDelayMaxSeconds { get; set; } = 6.0f;

    public float PartyFinderDelayMinSeconds { get; set; } = 3.0f;

    public float PartyFinderDelayMaxSeconds { get; set; } = 8.0f;

    public DelayRange DelayFor(HumanAction action) => action switch
    {
        HumanAction.MoveOff => new DelayRange(MoveOffDelayMinSeconds, MoveOffDelayMaxSeconds),
        HumanAction.Teleport => new DelayRange(TeleportDelayMinSeconds, TeleportDelayMaxSeconds),
        HumanAction.Engage => new DelayRange(EngageDelayMinSeconds, EngageDelayMaxSeconds),
        HumanAction.AcceptInvite => new DelayRange(InviteDelayMinSeconds, InviteDelayMaxSeconds),
        HumanAction.PartyFinder => new DelayRange(PartyFinderDelayMinSeconds, PartyFinderDelayMaxSeconds),
        _ => new DelayRange(LookingForGroupDelayMinSeconds, LookingForGroupDelayMaxSeconds),
    };

    public void SetDelay(HumanAction action, DelayRange range)
    {
        switch (action)
        {
            case HumanAction.MoveOff:
                (MoveOffDelayMinSeconds, MoveOffDelayMaxSeconds) = range;
                break;
            case HumanAction.Teleport:
                (TeleportDelayMinSeconds, TeleportDelayMaxSeconds) = range;
                break;
            case HumanAction.Engage:
                (EngageDelayMinSeconds, EngageDelayMaxSeconds) = range;
                break;
            case HumanAction.AcceptInvite:
                (InviteDelayMinSeconds, InviteDelayMaxSeconds) = range;
                break;
            case HumanAction.PartyFinder:
                (PartyFinderDelayMinSeconds, PartyFinderDelayMaxSeconds) = range;
                break;
            default:
                (LookingForGroupDelayMinSeconds, LookingForGroupDelayMaxSeconds) = range;
                break;
        }
    }
}
