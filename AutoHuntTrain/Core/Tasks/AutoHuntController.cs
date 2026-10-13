using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Game.Ops;
using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using clib.Services;

namespace AutoHuntTrain.Core.Tasks;

internal sealed partial class AutoHuntController
{
    private const int SessionSampleIntervalMs = 1_000;

    private readonly RideProgress progress = new();

    private AutoHuntSession? session;
    private Func<AutoHuntSession, AutoCommon>? rideTaskFactory;
    private AutoCommon? currentTask;
    private long nextSessionSampleAtMs;

    public bool Running => Svc.Automation.Running || Paused;

    public string Status => PauseReason switch
    {
        PauseReason.InContent => "Paused while you are in content",
        PauseReason.Manual    => "Paused",
        _                     => Svc.Automation.CurrentTask?.Status ?? "Idle",
    };

    public HuntPhase Phase => progress.Phase;

    // A ride under way takes a party, since party members share the credit on a mark; the chain after it, a pause and a
    // data center transfer do not, because the game refuses the transfer to a party member.
    public bool OpenToParty
        => session is { DataCenterTransferPending: false }
        && !Paused
        && Svc.Automation.Running
        && progress.Phase is not (HuntPhase.Idle or HuntPhase.Finishing);

    // A party member's teleport is taken only by a flag ride under way: never by an S rank hunt or the chain after a ride.
    public bool OpenToTeleportOffers => OpenToParty && currentTask is AutoRide;

    public bool RideAwaitsTeleport => currentTask is AutoRide { AwaitsTeleport: true };

    public RideProgress Progress => progress;

    public AutoHuntSession? SessionSnapshot => session;

    // The zone of the flag waiting to be followed or being travelled to, if any.
    public bool TryGetFlagZone(out uint territoryId)
    {
        if (currentTask is AutoRide ride)
        {
            return ride.TryGetHeadingZone(out territoryId);
        }

        territoryId = 0;
        return false;
    }

    public void NoteTeleportOfferAccepted(uint territoryId)
    {
        if (currentTask is AutoRide ride)
        {
            ride.NoteTeleportOfferAccepted(territoryId);
        }
    }

    private static void Diag(string message)
        => RunLog.Info(message);

    public void Start()
    {
        if (Running)
        {
            Diag("Start ignored: a ride is already running.");
            return;
        }

        if (!RequiredPluginsReady())
        {
            return;
        }

        var conductor = Conductor.Current;
        if (!conductor.IsSet)
        {
            Diag("Start aborted: no conductor is set.");
            ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Pick a conductor first, on the Train page or with /aht conductor First Last.");
            return;
        }

        var name = Conductor.Describe(conductor);
        BeginRun(new AutoHuntSession(), owning => new AutoRide(owning, progress, Plugin.Instance.Flags), $"following {name}");
        RideChat.PrintStart($"{AhtConstants.LogPrefix} Following {name}'s flags.");
    }

    // A ride from the feed: the journey to the train's start, then the follow loop. Only what makes a ride impossible
    // refuses it here, whether the player clicked or auto-join chose; the list a button was clicked on can be seconds
    // old, so it is checked once more.
    public bool StartRide(Announcement announcement)
    {
        var group = ExpansionGroups.Name(announcement.Group);
        var world = announcement.World;
        if (Running)
        {
            Diag($"Ride of the {group} train on {world.Name} ignored: a ride is already running.");
            return false;
        }

        if (!RequiredPluginsReady())
        {
            return false;
        }

        var verdict = RideRules.EvaluateManual(announcement, out _);
        if (verdict != RideVerdict.Rideable)
        {
            Diag($"Ride of the {group} train on {world.Name} refused: {RideRules.Explain(verdict)}.");
            ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Cannot ride the {group} train on {world.Name}: {RideRules.Explain(verdict)}.");
            return false;
        }

        RideAnnouncement(announcement, NewRideSession(announcement), "riding");
        var travel = announcement.CatchUpDueAt(DateTime.UtcNow) && TrainRoutes.TryFor(announcement, out _)
            ? $"it is already in progress, so the ride catches up with it once on {world.Name}"
            : $"travelling to {StartText(announcement)}";
        RideChat.PrintStart($"{AhtConstants.LogPrefix} Riding the {group} train on {world.Name}; {travel}.");
        return true;
    }

    // A ride rebuilt at login after the data center transfer's relog: the rules are not asked again, because the ride
    // was committed to before the transfer and Lifestream is expected to be busy with it still.
    public bool ResumeRide(in PendingRide pending)
    {
        if (Running)
        {
            Diag($"Resume of the ride to {pending.WorldName} ignored: a task is already running.");
            return false;
        }

        if (!RequiredPluginsReady())
        {
            return false;
        }

        if (!pending.TryToAnnouncement(out var announcement))
        {
            Diag($"Resume of the ride to {pending.WorldName} refused: world {pending.WorldId} is not one this client knows.");
            return false;
        }

        var resumed = NewRideSession(announcement);
        resumed.Restore(pending.SessionStartedAtUtc, pending.MarksCredited);
        resumed.ArrivedAtTrain = pending.ArrivedAtTrain;
        resumed.CrossedDataCenter = true;
        resumed.ResumeJourney = true;
        RideAnnouncement(announcement, resumed, "resuming after the data center transfer");
        RideChat.PrintStart($"{AhtConstants.LogPrefix} Picking the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} back up after the data center transfer.");
        return true;
    }

    private static AutoHuntSession NewRideSession(in Announcement announcement)
    {
        var world = announcement.World;
        return new AutoHuntSession
        {
            WorldName = world.Name,
            DataCenterName = world.DataCenterName,
            Kind = announcement.Kind,
            Group = announcement.Group,
            Expansion = ExpansionGroups.ToExpansionKind(announcement.Group) ?? (announcement.NamesTerritory ? AutoRide.ExpansionOf(announcement.TerritoryId) : null),
        };
    }

    private void RideAnnouncement(Announcement announcement, AutoHuntSession newSession, string verb)
    {
        var world = announcement.World;
        lastRiddenAnnouncementId = announcement.Id;
        RememberTaken(announcement.Id);
        BeginRun(newSession, owning => new AutoRide(owning, progress, Plugin.Instance.Flags, announcement), $"{verb} the {ExpansionGroups.Name(announcement.Group)} train on {world.Name} ({world.DataCenterName}) starting {announcement.StartAtUtc:HH:mm}Z");
    }

    private static string StartText(in Announcement announcement)
    {
        if (announcement.NamesAetheryte && ZoneAetherytes.TryFindById(announcement.AetheryteId, out _, out var aetheryte))
        {
            return $"{aetheryte.Name} in {TerritoryNames.Of(announcement.TerritoryId)}";
        }

        return announcement.NamesTerritory ? TerritoryNames.Of(announcement.TerritoryId) : "the train's world";
    }

    // A GM nearby ends the ride as stopped, whatever it was about to become, and nothing runs after it: no leaving the
    // party, no way home, no after-run action.
    public void StopForAlert()
    {
        if (session is { Recorded: false } live)
        {
            live.Outcome = RideOutcome.Stopped;
        }

        Stop();
        // A GM nearby is the player's call to make; auto-join must not start the next train a second later.
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.IsAutoJoinActive())
        {
            return;
        }

        configuration.DisableAutoJoin();
        configuration.SaveDebounced();
        Diag("GM alert: auto-join switched off for every expansion until the player turns it back on.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Auto-join is off after the GM alert; turn it back on in Settings when you are ready.");
    }

    public void Stop()
    {
        var ending = session;
        var wasPaused = Paused;
        var flagsFollowed = progress.FlagsFollowed;
        currentTask = null;
        PauseReason = PauseReason.None;
        Svc.Automation.Stop();
        if (ending is not null)
        {
            ending.Outcome ??= RideOutcome.Stopped;
            ReleaseHelpers();
            AbandonTravel();
        }

        // Pause already credited the run, and anything done since was the player's own play.
        FinalizeRun(ending, sample: !wasPaused);
        ClearRun();
        if (ending is null)
        {
            return;
        }

        Diag($"Stop requested after {flagsFollowed} flag(s); session cleared.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Ride stopped after {flagsFollowed} flag(s).");
    }

    // Credits seals as they land, so the stat tiles keep pace with the wallet. A paused run is left alone, because
    // Resume makes the game state its new zero point.
    public void Tick()
    {
        var now = Environment.TickCount64;
        TickAutoRide(now);
        if (session is null || session.Recorded || Paused)
        {
            return;
        }

        if (now < nextSessionSampleAtMs)
        {
            return;
        }

        nextSessionSampleAtMs = now + SessionSampleIntervalMs;
        session.Sample();
    }

    private static bool RequiredPluginsReady()
    {
        if (ExternalPlugins.AllRequiredInstalled())
        {
            return true;
        }

        var missing = ExternalPlugins.MissingRequiredNames();
        Diag($"Start aborted: required plugins missing ({missing}).");
        ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Cannot start: install all required plugins first ({missing}).");
        return false;
    }

    // Every Resume and fault restart builds the ride task afresh from the same factory.
    private void BeginRun(AutoHuntSession newSession, Func<AutoHuntSession, AutoCommon> taskFactory, string plan)
    {
        PauseReason = PauseReason.None;
        ResetFaultBudget();
        session = newSession;
        newSession.InPartyAtStart = PartyOps.InParty();
        rideTaskFactory = taskFactory;
        Diag($"Run starting: {plan}, job {newSession.JobAbbreviation}.");
        RunRide(newSession);
    }

    private void RunRide(AutoHuntSession owningSession)
    {
        progress.Reset();
        progress.SetPhase(HuntPhase.Preparing);
        RunTask(rideTaskFactory!(owningSession), () => OnRideEnded(owningSession));
    }

    private bool CanRestart() => rideTaskFactory is not null;

    // A stopped ride is over for good: neither record may bring it back at the next login, and a transfer Lifestream
    // still has in hand would otherwise carry the character off after the Stop.
    private static void AbandonTravel()
    {
        var configuration = Plugin.Instance.Configuration;
        configuration.ClearPendingRide();
        configuration.ClearPendingJourney();
        var lifestream = LifestreamIPC.Instance;
        if (!lifestream.IsBusy())
        {
            return;
        }

        lifestream.Abort();
        Diag("Stop: Lifestream was busy and its travel was aborted with the ride.");
    }

    // The movement library fires OnCompleted off the game thread: its await of the task does not return to the framework
    // scheduler, and the runtime moves the continuation to the thread pool. Recording a run reads the object table, which
    // Dalamud allows only on the game thread, so the hand-off is moved back there.
    private void RunTask(AutoCommon task, Action onCompleted)
    {
        currentTask = task;
        Svc.Automation.Start(task, OnCompleted: () => _ = ECommons.DalamudServices.Svc.Framework.RunOnFrameworkThread(() => HandOff(task, onCompleted)));
    }

    private void HandOff(AutoCommon task, Action onCompleted)
    {
        if (!ReferenceEquals(currentTask, task))
        {
            Diag($"{task.GetType().Name} finished but is no longer the current task (stopped, paused, or superseded); skipping hand-off.");
            return;
        }

        currentTask = null;
        onCompleted();
    }

    private void ClearRun()
    {
        session = null;
        rideTaskFactory = null;
        progress.Reset();
    }

    // A stopped task unwinds on a later frame, and after an unload that frame may never come, so the combat preset and
    // the pathfinder are released here as well.
    private static void ReleaseHelpers()
    {
        BossModIPC.Instance.ClearActive();
        BossModIPC.Instance.ReleaseMovement();
        NavmeshIPC.Instance.Stop();
    }
}

internal enum HuntPhase { Idle, Preparing, Waiting, Travelling, Searching, Fighting, Upkeep, Finishing, Paused }
