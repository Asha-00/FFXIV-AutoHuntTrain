using AutoHuntTrain.Core;
using AutoHuntTrain.Core.Debug;
using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Game;
using AutoHuntTrain.Core.Game.Watchers;
using AutoHuntTrain.Core.Kills;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Windows;
using AutoHuntTrain.Windows.Shell;
using clib;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace AutoHuntTrain;

public sealed class Plugin : IDalamudPlugin
{
    private const string GotoSubcommand = "goto";
    private const string ConductorSubcommand = "conductor";
    private const string SnoozeSubcommand = "snooze";
    private const string InjectSubcommand = "inject";
    private const string NavmeshIpcProviderMarker = "Navmesh.IPCProvider";
    private const uint OpenTrainLinkCommandId = 1;
    // A train's details link carries its announcement id as this plus the id, since a link hands its handler nothing else.
    private const uint TrainDetailsLinkBase = 1_000;
    // Lifestream's own budget for a data center transfer is up to an hour; a plan older than this is a leftover, not a journey in flight.
    private static readonly TimeSpan PendingJourneyMaxAge = TimeSpan.FromMinutes(90);
    private static readonly TimeSpan PendingRideMaxAge = TimeSpan.FromMinutes(90);

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;

    internal static KillLedger Kills { get; private set; } = null!;

    internal Configuration Configuration { get; }
    internal WindowSystem WindowSystem { get; } = new("AutoHuntTrain");
    internal RunHistory History { get; }
    internal AutoHuntController Controller { get; }
    internal FlagListener Flags { get; }
    internal FeedListener Feed { get; }
    internal TrainNotifier Notifier { get; }
    internal TrainChatAlert ChatAlert { get; }
    internal PartyFinderOpener PartyFinder { get; } = new();
    internal DalamudLinkPayload OpenTrainLink { get; }

    private readonly DutyWatcher dutyWatcher;
    private readonly GmAlertWatcher gmAlertWatcher;
    private readonly PartyInviteWatcher partyInviteWatcher;
    private readonly AppWindow appWindow;
    private readonly NavArrowWindow navArrowWindow = new();
    private readonly CommandInfo primaryCommand;
    private readonly CommandInfo aliasCommand;
    private readonly Action<uint, SeString> onTrainDetailsLink;
    private int lastLinkedTrainId;

    public Plugin()
    {
        Instance = this;

        ECommonsMain.Init(PluginInterface, this);
        CLibMain.Init(PluginInterface, this, CLibModule.Automation);
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? Configuration.CreateFresh();
        if (Configuration.Migrate())
        {
            Configuration.Save();
        }

        OpenTrainLink = Svc.Chat.AddChatLinkHandler(OpenTrainLinkCommandId, OnOpenTrainLink);
        History = new RunHistory();
        Controller = new AutoHuntController();
        Flags = new FlagListener();
        Feed = new FeedListener();
        Notifier = new TrainNotifier(Feed);
        ChatAlert = new TrainChatAlert(Feed);
        onTrainDetailsLink = OnTrainDetailsLink;
        dutyWatcher = new DutyWatcher();
        gmAlertWatcher = new GmAlertWatcher();
        partyInviteWatcher = new PartyInviteWatcher();
        Kills = new KillLedger();

        InitializeLocalization();
        Fonts.Initialize(PluginInterface.UiBuilder, PluginDirectory);
        appWindow = new AppWindow(this);
        WindowSystem.AddWindow(appWindow);
        WindowSystem.AddWindow(navArrowWindow);

        primaryCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelp) };
        aliasCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelpAlias) };
        CommandManager.AddHandler(AhtConstants.PrimaryCommand, primaryCommand);
        CommandManager.AddHandler(AhtConstants.AliasCommand, aliasCommand);

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.ClientState.Login += OnLogin;
        if (Svc.ClientState.IsLoggedIn)
        {
            OnLogin();
        }
    }

    private static string PluginDirectory => PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty;

    public void Dispose()
    {
        Controller.Stop();
        Core.Ipc.DodgeTuning.Release();
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;

        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.ClientState.Login -= OnLogin;
        Configuration.SaveIfPending();

        WindowSystem.RemoveAllWindows();
        appWindow.Dispose();
        Fonts.Dispose();

        CommandManager.RemoveHandler(AhtConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AhtConstants.AliasCommand);

        Svc.Chat.RemoveChatLinkHandler();
        dutyWatcher.Dispose();
        gmAlertWatcher.Dispose();
        partyInviteWatcher.Dispose();
        Notifier.Dispose();
        ChatAlert.Dispose();
        Feed.Dispose();
        Flags.Dispose();
        Kills.Dispose();

        CLibMain.Dispose();
        ECommonsMain.Dispose();
    }

    public void OnLanguageChanged()
    {
        primaryCommand.HelpMessage = Loc.T(L.Plugin.CommandHelp);
        aliasCommand.HelpMessage = Loc.T(L.Plugin.CommandHelpAlias);
    }

    public void ToggleMainUi() => appWindow.Toggle();

    public void ToggleConfigUi() => appWindow.TogglePage(AppWindow.Page.Settings);

    public void ToggleAboutUi() => appWindow.TogglePage(AppWindow.Page.About);

    public void ToggleDependenciesUi() => appWindow.TogglePage(AppWindow.Page.Plugins);

    public void ToggleHistoryUi() => appWindow.TogglePage(AppWindow.Page.History);

    public void ToggleLogUi() => appWindow.TogglePage(AppWindow.Page.Log);

    public void ToggleChangelogUi() => appWindow.TogglePage(AppWindow.Page.Changelog);

    public void ShowTrainPage() => appWindow.Show(AppWindow.Page.Train);

    public void ShowTrainDetails(int announcementId) => appWindow.ShowTrainDetails(announcementId);

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
        }
        else if (trimmed.Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            ToggleAboutUi();
        }
        else if (trimmed.Equals("deps", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("dependencies", StringComparison.OrdinalIgnoreCase))
        {
            ToggleDependenciesUi();
        }
        else if (trimmed.Equals("stats", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("history", StringComparison.OrdinalIgnoreCase))
        {
            ToggleHistoryUi();
        }
        else if (trimmed.Equals("log", StringComparison.OrdinalIgnoreCase))
        {
            ToggleLogUi();
        }
        else if (trimmed.Equals("changelog", StringComparison.OrdinalIgnoreCase))
        {
            ToggleChangelogUi();
        }
        else if (trimmed.Equals("pause", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("resume", StringComparison.OrdinalIgnoreCase))
        {
            Controller.TogglePause();
        }
        else if (trimmed.Equals("target", StringComparison.OrdinalIgnoreCase))
        {
            TargetDumper.Dump();
        }
        else if (trimmed.Equals("marks", StringComparison.OrdinalIgnoreCase))
        {
            HuntMarkDumper.Dump();
        }
        else if (HasSubcommand(trimmed, GotoSubcommand))
        {
            AutoGoto.HandleCommand(trimmed[GotoSubcommand.Length..].Trim(), Controller.Running);
        }
        else if (HasSubcommand(trimmed, ConductorSubcommand))
        {
            Conductor.HandleCommand(trimmed[ConductorSubcommand.Length..].Trim());
        }
        else if (HasSubcommand(trimmed, SnoozeSubcommand))
        {
            Snooze.HandleCommand(trimmed[SnoozeSubcommand.Length..].Trim());
        }
        else if (HasSubcommand(trimmed, InjectSubcommand))
        {
            FeedInjector.HandleCommand(trimmed[InjectSubcommand.Length..].Trim(), Feed);
        }
        else
        {
            ToggleMainUi();
        }
    }

    // A train's link is registered with its chat line. Ids only grow, so a train is linked once; asked again, the
    // Train page link stands in rather than registering the same id twice.
    internal DalamudLinkPayload TrainDetailsLink(int announcementId)
    {
        if (announcementId <= lastLinkedTrainId)
        {
            return OpenTrainLink;
        }

        lastLinkedTrainId = announcementId;
        return Svc.Chat.AddChatLinkHandler(TrainDetailsLinkBase + (uint)announcementId, onTrainDetailsLink);
    }

    private void OnOpenTrainLink(uint commandId, SeString message) => ShowTrainPage();

    // A train that has left the feed since its line was written opens the Train page instead.
    private void OnTrainDetailsLink(uint commandId, SeString message)
    {
        var announcementId = (int)(commandId - TrainDetailsLinkBase);
        if (Feed.TryFind(announcementId, out _))
        {
            ShowTrainDetails(announcementId);
            return;
        }

        ShowTrainPage();
    }

    private void OnDraw()
    {
        WindowSystem.Draw();
        Configuration.FlushPendingSave();
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        Feed.Tick();
        Notifier.Tick();
        PartyFinder.Tick();
        Controller.Tick();
    }

    private static bool HasSubcommand(string arguments, string subcommand)
        => arguments.StartsWith(subcommand, StringComparison.OrdinalIgnoreCase)
        && (arguments.Length == subcommand.Length || char.IsWhiteSpace(arguments[subcommand.Length]));

    // The navmesh plugin answers pathfind IPC on fire-and-forget tasks this plugin never gets a handle to. When one
    // faults, typically a query issued while the zone mesh is still building, the finalizer would rethrow it as noise.
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        if (eventArgs.Observed)
        {
            return;
        }

        if (!eventArgs.Exception.ToString().Contains(NavmeshIpcProviderMarker, StringComparison.Ordinal))
        {
            return;
        }

        eventArgs.SetObserved();
        RunLog.Debug($"Observed a navmesh IPC task fault: {eventArgs.Exception.GetBaseException().Message}");
    }

    private void InitializeLocalization()
    {
        var directory = Path.Combine(PluginDirectory, "Localization");
        if (string.IsNullOrEmpty(Configuration.Language))
        {
            Configuration.Language = DetectLanguage();
            Configuration.Save();
        }

        Loc.Initialize(Configuration.Language, directory);
    }

    private static string DetectLanguage()
    {
        var dalamudLanguage = PluginInterface.UiLanguage;
        if (Languages.IsKnown(dalamudLanguage))
        {
            return Languages.Resolve(dalamudLanguage).Code;
        }

        switch (Svc.ClientState.ClientLanguage)
        {
            case Dalamud.Game.ClientLanguage.German: return Languages.German.Code;
            case Dalamud.Game.ClientLanguage.French: return Languages.French.Code;
            case Dalamud.Game.ClientLanguage.Japanese: return Languages.Japanese.Code;
        }

        var osLanguage = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        return Languages.IsKnown(osLanguage) ? Languages.Resolve(osLanguage).Code : Languages.English.Code;
    }

    private void OnLogin()
    {
        ResumePendingTravel();
        if (!Configuration.AutoShowOnLogin)
        {
            return;
        }

        appWindow.Show(AppWindow.Page.Train);
    }

    // A data center transfer logs the character out; when the task did not live through that, what it wrote first
    // picks it up here: the whole ride when a ride made the transfer, else the journey alone. A task that did live
    // through it keeps the ride and the journey for itself.
    private void ResumePendingTravel()
    {
        if (Configuration.PendingRide is null && Configuration.PendingJourney is null)
        {
            return;
        }

        if (Controller.Running)
        {
            RunLog.Info("Travel is pending and a task is already running; leaving it to that task.");
            return;
        }

        if (!ResumePendingRide())
        {
            ResumePendingJourney();
        }
    }

    // A ride is rebuilt only while its journey is still pending: a ride saved without one had already arrived, and
    // a relog after that is the player's own, not the transfer's.
    private bool ResumePendingRide()
    {
        if (Configuration.PendingRide is not { } ride)
        {
            return false;
        }

        var age = DateTime.UtcNow - ride.SavedAtUtc;
        if (age > PendingRideMaxAge)
        {
            RunLog.Info($"Dropping the saved ride to {ride.WorldName}: it was saved {age.TotalMinutes:F0} minutes ago.");
            Configuration.ClearPendingRide();
            return false;
        }

        if (Configuration.PendingJourney is not { ReturnTrip: false } plan || !string.Equals(plan.World, ride.WorldName, StringComparison.OrdinalIgnoreCase))
        {
            RunLog.Info($"Dropping the saved ride to {ride.WorldName}: no journey to it is pending.");
            Configuration.ClearPendingRide();
            return false;
        }

        RunLog.Info($"Resuming the ride to {ride.WorldName} after login.");
        if (Controller.ResumeRide(ride))
        {
            return true;
        }

        Configuration.ClearPendingRide();
        return false;
    }

    private void ResumePendingJourney()
    {
        if (Configuration.PendingJourney is not { } plan)
        {
            return;
        }

        var age = DateTime.UtcNow - plan.StartedAtUtc;
        if (age > PendingJourneyMaxAge)
        {
            RunLog.Info($"Dropping the pending journey to {plan.World}: it started {age.TotalMinutes:F0} minutes ago.");
            Configuration.ClearPendingJourney();
            return;
        }

        RunLog.Info($"Resuming the journey to {plan.World} after login.");
        AutoJourney.Resume(plan);
    }
}
