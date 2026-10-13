using AutoHuntTrain.Core.Game.Watchers;
using AutoHuntTrain.Core.Localization;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.Automation;
using ECommons.Automation.UIInput;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Text;

namespace AutoHuntTrain.Core.Game;

internal enum ListingOutcome : byte { None, Listed, Failed, Withdrawn }

// Puts a public listing up in the Party Finder's The Hunt category and takes it down again through the game's own
// windows, the way a player would: Recruit Members, the recruitment criteria and Recruit, later the listing's details
// and End Recruitment. The category goes through the criteria window's own callback, so the game applies its defaults
// for The Hunt; the comment and the password are written into the stored criteria the game rebuilds the window from
// and sends. Driven from the framework tick one step at a time, every step with a short limit: a step that fails closes
// the windows it opened and gives up, and a ride only ever waits for Recruit to be pressed.
internal sealed unsafe class PartyFinderListing : IDisposable
{
    // The stored comment is 192 bytes long and keeps its terminator inside them.
    public const int CommentMaxBytes = 191;

    private const string FinderAddonName = "LookingForGroup";
    private const string CriteriaAddonName = "LookingForGroupCondition";
    private const string DetailsAddonName = "LookingForGroupDetail";
    private const string PromptAddonName = "SelectYesno";
    private const string OpenCommand = "/partyfinder";
    // Button node ids, as ECommons' AddonMaster knows them. The finder's button reads View Details while a listing is up.
    private const uint RecruitMembersButtonId = 46;
    private const uint NormalPartyButtonId = 3;
    private const uint RecruitButtonId = 113;
    private const uint EndRecruitmentButtonId = 110;
    // Callback 12 of the criteria window picks a duty category by its bit index, and The Hunt is bit 11.
    private const int PickCategoryCallback = 12;
    private const int TheHuntCategoryIndex = 11;
    // The game's value for a listing without a password, which is what keeps it public.
    private const ushort NoPassword = 10_000;
    private const byte NormalPartyType = 0;
    private const int ClickSpacingMs = 500;
    // A click that opens a window waits this long for it before it is tried again.
    private const int OpenRetryMs = 1_500;
    private const int OpenTimeoutMs = 5_000;
    private const int StepTimeoutMs = 4_000;
    private const int RebuildSettleMs = 300;
    private const int ListedTimeoutMs = 8_000;
    private const int EndedTimeoutMs = 6_000;
    // Typing or a fight holds an end back; past this the listing is left for the player to end.
    private const int EndHoldLimitMs = 2 * TimeUnits.MillisecondsPerMinute;
    // The game drops a listing once the party is full or its time runs out; one gone this long has ended.
    private const int GoneConfirmMs = 3_000;
    // A listing that shows up this soon after Recruit was pressed is still the plugin's, even past the step's limit.
    private const int LateListingGraceMs = 30_000;

    private enum Step : byte
    {
        Idle,
        OpenFinder,
        OpenCriteria,
        PickNormalParty,
        PickTheHunt,
        FillCriteria,
        PressRecruit,
        AwaitListed,
        AwaitEndHold,
        OpenFinderToEnd,
        OpenDetails,
        PressEndRecruitment,
        AwaitEnded,
    }

    private enum Notice : byte { None, Commenced, Ended, NothingToEnd, Refused }

    private readonly byte[] comment = new byte[CommentMaxBytes];
    private readonly IChatGui.OnLogMessageDelegate onLogMessage;

    private int commentLength;
    private Step step;
    private long deadlineMs;
    private long waitUntilMs;
    private long lastClickAtMs;
    private long committedAtMs;
    private long goneSinceMs;
    private uint answeredPromptId;
    private uint refusalMessageId;
    private bool openedFinder;
    private bool endRequested;
    private bool ownsListing;
    private bool commencedByMessage;
    private bool nothingToEnd;
    private string endReason = string.Empty;
    private string endHold = string.Empty;

    public PartyFinderListing()
    {
        onLogMessage = OnLogMessage;
        Svc.Chat.LogMessage += onLogMessage;
    }

    public ListingOutcome Outcome { get; private set; }

    public bool Busy => step != Step.Idle;

    // Until Recruit is pressed the listing can still be withdrawn; whatever goes up after it is the plugin's to end.
    public bool AwaitingRecruit => step >= Step.OpenFinder && step <= Step.PressRecruit;

    // The game raises these conditions while the character's own listing is up, and refuses to start a duty under
    // either. Its own "recruitment commenced" message stands in for them after a Recruit the plugin pressed.
    public bool ListingUp
        => Svc.Condition[ConditionFlag.UsingPartyFinder] || Svc.Condition[ConditionFlag.RecruitingWorldOnly] || commencedByMessage;

    // Why the game's windows are better left alone right now, or null when nothing holds them back.
    public static string? Hold()
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            return "not logged in";
        }

        if (TextInputFocus.Active())
        {
            return "a text input has focus";
        }

        if (Svc.Condition[ConditionFlag.InCombat])
        {
            return "in combat";
        }

        if (DutyWatcher.InContent())
        {
            return "in a duty";
        }

        return GenericHelpers.IsOccupied() ? "busy with a cutscene, a loading screen, an event or the like" : null;
    }

    public bool TryBegin(string text, out string refused)
    {
        refused = Refusal();
        if (refused.Length > 0)
        {
            return false;
        }

        commentLength = EncodeComment(text, comment);
        Outcome = ListingOutcome.None;
        endRequested = false;
        openedFinder = false;
        answeredPromptId = 0;
        refusalMessageId = 0;
        committedAtMs = 0;
        lastClickAtMs = 0;
        RunLog.Info($"Party Finder: putting up a listing in The Hunt with the comment \"{CommentText()}\" ({commentLength} bytes)");
        Enter(Step.OpenFinder, OpenTimeoutMs);
        return true;
    }

    public void Withdraw(string reason)
    {
        if (!AwaitingRecruit)
        {
            return;
        }

        RunLog.Info($"Party Finder: the listing is withdrawn before Recruit was pressed, as {reason}");
        Outcome = ListingOutcome.Withdrawn;
        StandDown();
    }

    // Only a listing the plugin put up is ended; one the player had up before is never touched.
    public void RequestEnd(string reason)
    {
        if (AwaitingRecruit)
        {
            Withdraw(reason);
            return;
        }

        if (step == Step.AwaitListed)
        {
            if (!endRequested)
            {
                endRequested = true;
                endReason = reason;
                RunLog.Info($"Party Finder: {reason} while the listing was going up; it is ended once it is up");
            }

            return;
        }

        if (step != Step.Idle || !ownsListing)
        {
            return;
        }

        if (!ListingUp)
        {
            ownsListing = false;
            RunLog.Info($"Party Finder: {reason}, and the listing had already ended on its own");
            return;
        }

        BeginEnd(reason);
    }

    public void Tick()
    {
        if (!Svc.ClientState.IsLoggedIn)
        {
            commencedByMessage = false;
        }

        if (step == Step.Idle)
        {
            WatchOwnListing();
            return;
        }

        var now = Environment.TickCount64;
        try
        {
            Advance(now);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Party Finder: the step {step} threw");
            Fail("an unexpected error");
        }
    }

    public void Dispose()
    {
        Svc.Chat.LogMessage -= onLogMessage;
        if (Busy)
        {
            CloseOpenedWindows();
            step = Step.Idle;
        }

        if (!ownsListing || !ListingUp)
        {
            return;
        }

        RunLog.Warning("Party Finder: unloading with the listing still up; it is left for the player to end");
        PrintChat(L.Safety.PartyFinderStillUpChat);
    }

    // Control characters would break the stored line, and the game's font has no glyphs past the Basic Multilingual
    // Plane, so both are dropped; the rest is cut at a character boundary to fit.
    private static int EncodeComment(string text, Span<byte> destination)
    {
        var trimmed = text.AsSpan().Trim();
        var written = 0;
        for (var charIndex = 0; charIndex < trimmed.Length; charIndex++)
        {
            var character = trimmed[charIndex];
            if (char.IsControl(character) || char.IsSurrogate(character))
            {
                continue;
            }

            var single = trimmed.Slice(charIndex, 1);
            if (written + Encoding.UTF8.GetByteCount(single) > destination.Length)
            {
                break;
            }

            written += Encoding.UTF8.GetBytes(single, destination[written..]);
        }

        return written;
    }

    // The LogMessage rows the game prints about party recruitment, matched by id so every client language reads alike:
    // commenced, ended (by hand, full, timed out, cancelled, not continued), nothing to end, and the refusals.
    private static Notice Classify(uint logMessageId) => logMessageId switch
    {
        979 or 980 => Notice.Commenced,
        981 or 982 or 983 or 984 or 985 or 986 or 987 or 988 or 993 or 994 => Notice.Ended,
        966 or 977 => Notice.NothingToEnd,
        954 or 956 or 957 or 959 or 961 or 962 or 963 or 964 or 965 or 967 or 974 or 978 or 1026 or 1027 or 1436 or 1437 or 7463 => Notice.Refused,
        _ => Notice.None,
    };

    private void OnLogMessage(ILogMessage message)
    {
        var messageId = message.LogMessageId;
        var notice = Classify(messageId);
        if (notice == Notice.None)
        {
            return;
        }

        RunLog.Info($"Party Finder: the game says \"{MessageText(message)}\" (LogMessage {messageId}) during {step}");
        switch (notice)
        {
            case Notice.Commenced:
                if (committedAtMs != 0 && Environment.TickCount64 - committedAtMs < LateListingGraceMs)
                {
                    commencedByMessage = true;
                }

                break;
            case Notice.Ended:
                commencedByMessage = false;
                break;
            case Notice.NothingToEnd:
                commencedByMessage = false;
                nothingToEnd = step >= Step.AwaitEndHold;
                break;
            case Notice.Refused:
                if (step is Step.PressRecruit or Step.AwaitListed or Step.PressEndRecruitment or Step.AwaitEnded)
                {
                    refusalMessageId = messageId;
                }

                break;
        }
    }

    private string Refusal()
    {
        if (Busy)
        {
            return "the Party Finder is still busy with the last listing";
        }

        if (ListingUp)
        {
            return "a listing is already up";
        }

        if (Hold() is { } hold)
        {
            return hold;
        }

        return AddonVisible(FinderAddonName) || AddonVisible(CriteriaAddonName) || AddonVisible(DetailsAddonName)
            ? "the Party Finder window is open"
            : string.Empty;
    }

    private void Advance(long now)
    {
        if (refusalMessageId != 0)
        {
            var messageId = refusalMessageId;
            refusalMessageId = 0;
            Fail($"the game refused it with LogMessage {messageId}");
            return;
        }

        if (now > deadlineMs)
        {
            Fail(TimeoutText());
            return;
        }

        if (now < waitUntilMs)
        {
            return;
        }

        switch (step)
        {
            case Step.OpenFinder:
                OpenFinder(Step.OpenCriteria);
                break;
            case Step.OpenCriteria:
                OpenCriteria(now);
                break;
            case Step.PickNormalParty:
                PickNormalParty(now);
                break;
            case Step.PickTheHunt:
                PickTheHunt(now);
                break;
            case Step.FillCriteria:
                FillCriteria(now);
                break;
            case Step.PressRecruit:
                PressRecruit(now);
                break;
            case Step.AwaitListed:
                AwaitListed(now);
                break;
            case Step.AwaitEndHold:
                AwaitEndHold();
                break;
            case Step.OpenFinderToEnd:
                if (!EndedMeanwhile())
                {
                    OpenFinder(Step.OpenDetails);
                }

                break;
            case Step.OpenDetails:
                OpenDetails(now);
                break;
            case Step.PressEndRecruitment:
                PressEndRecruitment(now);
                break;
            case Step.AwaitEnded:
                EndedMeanwhile();
                break;
        }
    }

    private string TimeoutText() => step switch
    {
        Step.OpenFinder or Step.OpenFinderToEnd => "the Party Finder window did not open",
        Step.OpenCriteria => "the recruitment criteria did not open",
        Step.PickNormalParty => "the recruitment could not be set to a normal party",
        Step.PickTheHunt => "The Hunt could not be picked as the duty category",
        Step.FillCriteria or Step.PressRecruit => "the Recruit button could not be pressed",
        Step.AwaitListed => "no listing appeared after Recruit; the chat log may say why",
        Step.AwaitEndHold => $"the end was held back for too long ({endHold})",
        Step.OpenDetails => "the listing's details did not open, or showed another listing",
        Step.PressEndRecruitment => "the End Recruitment button could not be pressed",
        _ => "the listing did not end",
    };

    // A finder the player had open is used as it is and left open afterwards.
    private void OpenFinder(Step next)
    {
        if (TryGetReady(FinderAddonName, out _))
        {
            RunLog.Debug($"Party Finder: the window is {(openedFinder ? "open" : "already open")}");
            Enter(next, OpenTimeoutMs);
            return;
        }

        if (openedFinder || AddonVisible(FinderAddonName))
        {
            return;
        }

        try
        {
            Chat.SendMessage(OpenCommand);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Party Finder: the open command could not be sent");
            Fail("the open command could not be sent");
            return;
        }

        openedFinder = true;
        RunLog.Debug("Party Finder: opening the window");
    }

    private void OpenCriteria(long now)
    {
        if (TryGetReady(CriteriaAddonName, out _))
        {
            RunLog.Debug("Party Finder: the recruitment criteria are open");
            Enter(Step.PickNormalParty, StepTimeoutMs);
            return;
        }

        if (AddonVisible(DetailsAddonName))
        {
            Fail("the Party Finder showed a listing's details instead of the recruitment criteria");
            return;
        }

        if (now - lastClickAtMs < OpenRetryMs || !TryGetReady(FinderAddonName, out var finder) || !PressButton(finder, RecruitMembersButtonId))
        {
            return;
        }

        lastClickAtMs = now;
        RunLog.Debug("Party Finder: pressed Recruit Members");
    }

    private void PickNormalParty(long now)
    {
        var agent = AgentOrFail();
        if (agent is null)
        {
            return;
        }

        if (agent->GroupTypeTab == NormalPartyType)
        {
            Enter(Step.PickTheHunt, StepTimeoutMs);
            return;
        }

        if (now - lastClickAtMs < OpenRetryMs || !TryGetReady(CriteriaAddonName, out var criteriaWindow) || !PressButton(criteriaWindow, NormalPartyButtonId))
        {
            return;
        }

        lastClickAtMs = now;
        RunLog.Info($"Party Finder: the stored criteria are for recruitment type {agent->GroupTypeTab}; switching to a normal party");
    }

    private void PickTheHunt(long now)
    {
        var agent = AgentOrFail();
        if (agent is null)
        {
            return;
        }

        var category = agent->StoredRecruitmentInfo.SelectedCategory;
        if (category == AgentLookingForGroup.DutyCategory.TheHunt)
        {
            Enter(Step.FillCriteria, StepTimeoutMs);
            return;
        }

        if (now - lastClickAtMs < OpenRetryMs || !TryGetReady(CriteriaAddonName, out var criteriaWindow))
        {
            return;
        }

        lastClickAtMs = now;
        RunLog.Info($"Party Finder: the stored criteria are for {category}; picking The Hunt");
        Callback.Fire(criteriaWindow, false, PickCategoryCallback, TheHuntCategoryIndex);
    }

    // The window is rebuilt from the stored criteria only when they changed, the way the game refreshes it itself.
    private void FillCriteria(long now)
    {
        var agent = AgentOrFail();
        if (agent is null)
        {
            return;
        }

        var criteria = &agent->StoredRecruitmentInfo;
        var rebuild = false;
        if (!CommentStored(criteria))
        {
            WriteComment(criteria);
            rebuild = true;
        }

        if (criteria->Password != NoPassword)
        {
            criteria->Password = NoPassword;
            rebuild = true;
            RunLog.Info("Party Finder: the stored criteria carried a password or a private party; the listing is made public");
        }

        Enter(Step.PressRecruit, StepTimeoutMs);
        if (!rebuild)
        {
            return;
        }

        agent->PopulateRecruitmentCriteriaPopup(false, false);
        waitUntilMs = now + RebuildSettleMs;
        RunLog.Debug("Party Finder: wrote the comment into the stored criteria and rebuilt the criteria window from them");
    }

    private void PressRecruit(long now)
    {
        var agent = AgentOrFail();
        if (agent is null || !TryGetReady(CriteriaAddonName, out var criteriaWindow) || now - lastClickAtMs < ClickSpacingMs)
        {
            return;
        }

        if (agent->StoredRecruitmentInfo.SelectedCategory != AgentLookingForGroup.DutyCategory.TheHunt || !CommentStored(&agent->StoredRecruitmentInfo))
        {
            Fail("the criteria changed after they were filled in");
            return;
        }

        if (!PressButton(criteriaWindow, RecruitButtonId))
        {
            return;
        }

        lastClickAtMs = now;
        committedAtMs = now;
        goneSinceMs = 0;
        RunLog.Info("Party Finder: pressed Recruit");
        Enter(Step.AwaitListed, ListedTimeoutMs);
    }

    private void AwaitListed(long now)
    {
        if (!ListingUp)
        {
            AnswerConfirmation(now);
            return;
        }

        ownsListing = true;
        committedAtMs = 0;
        Outcome = ListingOutcome.Listed;
        RunLog.Info($"Party Finder: the listing is up in The Hunt{(answeredPromptId != 0 ? " after the game's confirmation" : string.Empty)}");
        StandDown();
        if (endRequested)
        {
            BeginEnd(endReason);
        }
    }

    // The game asks before some listings go up, a cross-world party for The Hunt among them. The agent keeps the id of
    // the prompt it raised, so no other prompt is ever answered.
    private void AnswerConfirmation(long now)
    {
        var agent = AgentLookingForGroup.Instance();
        if (agent is null || now - lastClickAtMs < ClickSpacingMs)
        {
            return;
        }

        var promptId = agent->JoinConfirmAddonId;
        if (promptId == 0 || promptId == answeredPromptId)
        {
            return;
        }

        var prompt = RaptureAtkUnitManager.Instance()->GetAddonById((ushort)promptId);
        if (prompt is null || !GenericHelpers.IsAddonReady(prompt) || prompt->NameString != PromptAddonName)
        {
            return;
        }

        var yesButton = ((AddonSelectYesno*)prompt)->YesButton;
        if (yesButton is null || !yesButton->IsEnabled)
        {
            return;
        }

        RunLog.Info($"Party Finder: the game asks to confirm the listing (\"{PromptText(prompt)}\"); answering yes");
        new AddonMaster.SelectYesno((nint)prompt) { RespectDisabledButtons = true }.Yes();
        answeredPromptId = promptId;
        lastClickAtMs = now;
    }

    private void BeginEnd(string reason)
    {
        endRequested = false;
        endReason = reason;
        endHold = string.Empty;
        openedFinder = false;
        nothingToEnd = false;
        refusalMessageId = 0;
        lastClickAtMs = 0;
        RunLog.Info($"Party Finder: ending the listing, as {reason}");
        Enter(Step.AwaitEndHold, EndHoldLimitMs);
    }

    private void AwaitEndHold()
    {
        if (EndedMeanwhile())
        {
            return;
        }

        if (Hold() is { } hold)
        {
            if (!string.Equals(hold, endHold, StringComparison.Ordinal))
            {
                endHold = hold;
                RunLog.Info($"Party Finder: the end of the listing waits: {hold}");
            }

            return;
        }

        Enter(Step.OpenFinderToEnd, OpenTimeoutMs);
    }

    // The details are trusted only once the game shows them for this character's own listing.
    private void OpenDetails(long now)
    {
        if (EndedMeanwhile())
        {
            return;
        }

        if (TryGetReady(DetailsAddonName, out _))
        {
            var agent = AgentOrFail();
            if (agent is not null && agent->ListingContentId != 0 && agent->ListingContentId == Svc.PlayerState.ContentId)
            {
                RunLog.Debug("Party Finder: the listing's details are open");
                Enter(Step.PressEndRecruitment, StepTimeoutMs);
            }

            return;
        }

        if (AddonVisible(CriteriaAddonName))
        {
            Fail("the Party Finder opened the recruitment criteria instead of the listing's details");
            return;
        }

        if (now - lastClickAtMs < OpenRetryMs || !TryGetReady(FinderAddonName, out var finder) || !PressButton(finder, RecruitMembersButtonId))
        {
            return;
        }

        lastClickAtMs = now;
        RunLog.Debug("Party Finder: pressed View Details");
    }

    private void PressEndRecruitment(long now)
    {
        if (EndedMeanwhile() || now - lastClickAtMs < ClickSpacingMs || !TryGetReady(DetailsAddonName, out var details) || !PressButton(details, EndRecruitmentButtonId))
        {
            return;
        }

        lastClickAtMs = now;
        RunLog.Info("Party Finder: pressed End Recruitment");
        Enter(Step.AwaitEnded, EndedTimeoutMs);
    }

    private bool EndedMeanwhile()
    {
        if (ListingUp && !nothingToEnd)
        {
            return false;
        }

        ownsListing = false;
        commencedByMessage = false;
        committedAtMs = 0;
        RunLog.Info(step == Step.AwaitEnded
            ? "Party Finder: the listing is ended"
            : "Party Finder: the listing had ended meanwhile, so nothing is left to end");
        StandDown();
        return true;
    }

    private void Fail(string why)
    {
        var ending = step >= Step.AwaitEndHold;
        RunLog.Warning($"Party Finder: {why}; {(ending ? "the listing is left up" : "no listing")}, and the windows opened for it are closed");
        StandDown();
        if (ending)
        {
            ownsListing = false;
            PrintChat(L.Safety.PartyFinderStillUpChat);
            return;
        }

        Outcome = ListingOutcome.Failed;
        PrintChat(L.Safety.PartyFinderFailedChat);
    }

    // A listing ends without the plugin once the party is full, its time runs out or the player ends it, and a listing
    // put up later is the player's own; one gone for a moment is no longer the plugin's to end. One that shows up just
    // after its step gave up waiting is still the plugin's.
    private void WatchOwnListing()
    {
        var now = Environment.TickCount64;
        if (!ownsListing)
        {
            if (committedAtMs == 0 || now - committedAtMs >= LateListingGraceMs || !ListingUp)
            {
                return;
            }

            ownsListing = true;
            committedAtMs = 0;
            goneSinceMs = 0;
            RunLog.Info("Party Finder: the listing went up after all; it is ended with the ride");
            if (endRequested)
            {
                BeginEnd(endReason);
            }

            return;
        }

        if (ListingUp)
        {
            goneSinceMs = 0;
            return;
        }

        if (goneSinceMs == 0)
        {
            goneSinceMs = now;
            return;
        }

        if (now - goneSinceMs < GoneConfirmMs)
        {
            return;
        }

        ownsListing = false;
        RunLog.Info("Party Finder: the listing has ended (the party filled up, its time ran out, or it was ended by hand)");
    }

    private void Enter(Step next, int timeoutMs)
    {
        step = next;
        deadlineMs = Environment.TickCount64 + timeoutMs;
        waitUntilMs = 0;
    }

    private void StandDown()
    {
        CloseOpenedWindows();
        step = Step.Idle;
    }

    private void CloseOpenedWindows()
    {
        if (!openedFinder)
        {
            return;
        }

        openedFinder = false;
        var agent = AgentLookingForGroup.Instance();
        if (agent is null || !agent->IsAgentActive())
        {
            return;
        }

        agent->Hide();
        RunLog.Debug("Party Finder: closed the windows opened for the listing");
    }

    private AgentLookingForGroup* AgentOrFail()
    {
        var agent = AgentLookingForGroup.Instance();
        if (agent is null)
        {
            Fail("the Party Finder agent is not available");
        }

        return agent;
    }

    private bool CommentStored(AgentLookingForGroup.RecruitmentSub* criteria)
    {
        var stored = criteria->Comment;
        return stored[commentLength] == 0 && stored[..commentLength].SequenceEqual(comment.AsSpan(0, commentLength));
    }

    private void WriteComment(AgentLookingForGroup.RecruitmentSub* criteria)
    {
        var stored = criteria->Comment;
        stored.Clear();
        comment.AsSpan(0, commentLength).CopyTo(stored);
    }

    private string CommentText() => Encoding.UTF8.GetString(comment, 0, commentLength);

    // A button the game has not drawn, has greyed out or has no click event on is never clicked. Radio buttons count
    // as buttons here, as they do for the game.
    private static bool PressButton(AtkUnitBase* addon, uint nodeId)
    {
        var button = addon->GetComponentButtonById(nodeId);
        if (button is null || !button->IsEnabled || !button->AtkResNode->IsVisible())
        {
            return false;
        }

        var owner = button->AtkComponentBase.OwnerNode;
        if (owner is null || owner->AtkResNode.AtkEventManager.Event is null)
        {
            return false;
        }

        button->ClickAddonButton(addon);
        return true;
    }

    private static bool TryGetReady(string addonName, out AtkUnitBase* addon)
        => GenericHelpers.TryGetAddonByName(addonName, out addon) && GenericHelpers.IsAddonReady(addon);

    private static bool AddonVisible(string addonName)
        => GenericHelpers.TryGetAddonByName<AtkUnitBase>(addonName, out var addon) && addon->IsVisible;

    private static string MessageText(ILogMessage message)
        => message.GameData.ValueNullable is { } row ? row.Text.ExtractText().Replace('\n', ' ') : string.Empty;

    private static string PromptText(AtkUnitBase* prompt)
    {
        try
        {
            var master = new AddonMaster.SelectYesno((nint)prompt);
            var text = master.Addon->PromptText is not null ? master.Text : master.TextLegacy;
            return text.Replace('\n', ' ');
        }
        catch (Exception exception)
        {
            RunLog.Debug($"Party Finder: the confirmation text could not be read: {exception.Message}");
            return string.Empty;
        }
    }

    private static void PrintChat(LocString line) => Svc.Chat.Print($"{AhtConstants.LogPrefix} {Loc.T(line)}");
}
