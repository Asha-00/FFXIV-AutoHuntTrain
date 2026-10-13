using AutoHuntTrain.Core.Game.Ops;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoHuntTrain.Core.Game.Watchers;

// A party member can take the party along on a teleport, and the game asks "X wishes to teleport you to Y." During a
// ride the offer is taken when it serves the train: into the zone of the flag waiting or being travelled to, or, while
// no flag is known, into a hunt zone. The prompt is matched against its Addon sheet template, so the check works in
// every client language, and the place it names is looked up as an aetheryte. An offer that comes during a fight, a
// cast, the ride's own teleport or its stop at a flag is held while the prompt stays open; any other is left for the
// player.
internal sealed unsafe class TeleportOfferWatcher : IDisposable
{
    private const ushort NoOffer = 0;
    private const uint TeleportOfferPromptRow = 166;
    private const int OffererPlaceholder = 0;
    private const int PlacePlaceholder = 1;
    private const int PromptPlaceholders = 2;
    // A prompt clicked the frame it opens can drop the click, so the accept waits at least this long even with the
    // humanizer off.
    private const int MinimumAcceptDelayMs = 500;
    private const int NotReadyAbandonMs = 15_000;
    // A fight or a stop at the flag that outlasts this leaves the offer to the player rather than taking it stale.
    private const int HoldLimitMs = 60_000;

    private PromptTemplate offerPrompt = PromptTemplate.Invalid;
    private bool templateLoaded;

    private ushort offerAddonId = NoOffer;
    private long acceptAtTick;
    private long holdLimitTick;
    private OfferCall heldFor = OfferCall.Accept;
    private string offerer = "";
    private ZoneAetheryte destination;
    private uint destinationTerritoryId;
    private bool destinationIsHuntZone;

    private enum OfferCall : byte
    {
        Accept,
        HoldAtFlag,
        HoldOwnTeleport,
        HoldBusy,
        LeaveClosed,
        LeaveJourney,
        LeaveOtherZone,
        LeaveAlreadyThere,
        LeaveNotHuntZone,
    }

    public TeleportOfferWatcher()
    {
        Svc.AddonLifecycle.RegisterListener(AddonEvent.PostSetup, SelectYesnoPrompt.AddonName, OnSelectYesnoSetup);
        Svc.Framework.Update += OnUpdate;
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnUpdate;
        Svc.AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, SelectYesnoPrompt.AddonName, OnSelectYesnoSetup);
    }

    private static bool Armed()
    {
        var plugin = Plugin.Instance;
        return plugin.Configuration.AcceptPartyTeleports && plugin.Controller.OpenToTeleportOffers;
    }

    private static bool IsHold(OfferCall call) => call is OfferCall.HoldAtFlag or OfferCall.HoldOwnTeleport or OfferCall.HoldBusy;

    private void OnSelectYesnoSetup(AddonEvent type, AddonArgs args)
    {
        var addon = (AtkUnitBase*)args.Addon.Address;
        if (addon is null)
        {
            return;
        }

        // A prompt set up again under the held offer's id is another question now, and a held offer can wait a minute.
        if (offerAddonId != NoOffer && addon->Id == offerAddonId)
        {
            RunLog.Info($"Teleport offer from {offerer}: its prompt was replaced before the ride answered it; standing down.");
            offerAddonId = NoOffer;
        }

        if (offerAddonId != NoOffer || !Armed())
        {
            return;
        }

        EnsureTemplate();
        var prompt = SelectYesnoPrompt.Read(addon);
        Span<Range> placeholders = stackalloc Range[PromptPlaceholders];
        if (!offerPrompt.TryMatch(prompt, placeholders))
        {
            RunLog.Debug($"SelectYesno {addon->Id} is not a teleport offer: \"{prompt}\"");
            return;
        }

        var shownOfferer = prompt[placeholders[OffererPlaceholder]];
        var place = prompt[placeholders[PlacePlaceholder]];
        if (!PartyOps.HasMemberShownAs(shownOfferer))
        {
            RunLog.Info($"Teleport offer from {shownOfferer} to {place} left for you to answer: they are not in the party.");
            return;
        }

        if (!ZoneAetherytes.TryFindByName(place, out destinationTerritoryId, out destination))
        {
            RunLog.Info($"Teleport offer from {shownOfferer} to {place} left for you to answer: no single aetheryte goes by that name.");
            return;
        }

        offerer = shownOfferer;
        destinationIsHuntZone = LeadsToHuntZone(destinationTerritoryId, destination.Id);
        offerAddonId = addon->Id;
        heldFor = OfferCall.Accept;
        var delayMs = Math.Max(MinimumAcceptDelayMs, Humanizer.DrawDelayMs(HumanAction.AcceptTeleport));
        acceptAtTick = Environment.TickCount64 + delayMs;
        holdLimitTick = acceptAtTick + HoldLimitMs;
        RunLog.Info($"Teleport offer from {offerer} to {DescribeDestination()} during the ride.");
        RunLog.Debug($"Teleport offer: waiting {delayMs} ms before {Humanizer.Describe(HumanAction.AcceptTeleport)}");
    }

    private void OnUpdate(IFramework _)
    {
        if (offerAddonId == NoOffer)
        {
            return;
        }

        var addon = SelectYesnoPrompt.Find(offerAddonId);
        if (addon is null)
        {
            RunLog.Info($"Teleport offer from {offerer}: the prompt closed before the ride answered it; standing down.");
            offerAddonId = NoOffer;
            return;
        }

        var now = Environment.TickCount64;
        if (now < acceptAtTick)
        {
            return;
        }

        var call = Judge();
        if (IsHold(call))
        {
            Hold(call, now);
            return;
        }

        if (call != OfferCall.Accept)
        {
            RunLog.Info($"Teleport offer from {offerer} to {DescribeDestination()} left for you to answer: {Describe(call)}.");
            offerAddonId = NoOffer;
            return;
        }

        if (heldFor != OfferCall.Accept)
        {
            RunLog.Info($"Teleport offer from {offerer}: no longer held ({Describe(heldFor)} is over).");
            heldFor = OfferCall.Accept;
            acceptAtTick = now;
        }

        if (!GenericHelpers.IsAddonReady(addon))
        {
            if (now >= acceptAtTick + NotReadyAbandonMs)
            {
                RunLog.Warning($"Teleport offer: prompt {addon->Id} never became ready; standing down.");
                offerAddonId = NoOffer;
            }

            return;
        }

        offerAddonId = NoOffer;
        try
        {
            new AddonMaster.SelectYesno((nint)addon).Yes();
            Plugin.Instance.Controller.NoteTeleportOfferAccepted(destinationTerritoryId);
            RunLog.Info($"Accepted the teleport offer from {offerer} to {DescribeDestination()} ({AutoCommon.ConditionTag()}).");
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Teleport offer: the accept click threw.");
        }
    }

    private void Hold(OfferCall call, long now)
    {
        if (now >= holdLimitTick)
        {
            RunLog.Info($"Teleport offer from {offerer} to {DescribeDestination()} left for you to answer: still held after {HoldLimitMs / TimeUnits.MillisecondsPerSecond}s, {Describe(call)}.");
            offerAddonId = NoOffer;
            return;
        }

        if (call == heldFor)
        {
            return;
        }

        heldFor = call;
        RunLog.Info($"Teleport offer from {offerer} held: {Describe(call)}.");
    }

    // Every check reads live state and allocates nothing, since it runs each frame while an offer is held.
    private OfferCall Judge()
    {
        if (!Armed())
        {
            return OfferCall.LeaveClosed;
        }

        var controller = Plugin.Instance.Controller;
        var phase = controller.Progress.RidePhase;
        if (phase is RidePhase.None or RidePhase.Journey or RidePhase.CatchingUp)
        {
            return OfferCall.LeaveJourney;
        }

        if (controller.TryGetFlagZone(out var flagTerritoryId))
        {
            if (!LeadsInto(flagTerritoryId))
            {
                return OfferCall.LeaveOtherZone;
            }

            if (Svc.ClientState.TerritoryType == flagTerritoryId)
            {
                return OfferCall.LeaveAlreadyThere;
            }
        }
        else if (!destinationIsHuntZone)
        {
            return OfferCall.LeaveNotHuntZone;
        }

        if (phase is RidePhase.AtFlag or RidePhase.WaitingForMark or RidePhase.Engaging)
        {
            return OfferCall.HoldAtFlag;
        }

        // A leg makes its own way unless it is waiting for someone else's teleport, and a wait that ran out is followed by
        // the leg's own teleport; two teleports at once refuse each other.
        if (phase == RidePhase.Travelling && !controller.RideAwaitsTeleport)
        {
            return OfferCall.HoldOwnTeleport;
        }

        return Busy() ? OfferCall.HoldBusy : OfferCall.Accept;
    }

    // The game refuses the teleport in a fight, and a cast or a zone change already under way would swallow it.
    private static bool Busy()
    {
        var condition = Svc.Condition;
        return condition[ConditionFlag.InCombat]
            || condition[ConditionFlag.Casting]
            || condition[ConditionFlag.Casting87]
            || condition[ConditionFlag.BetweenAreas]
            || condition[ConditionFlag.BetweenAreas51];
    }

    // A zone owning no aetheryte is reached through its gateway, so an offer to that aetheryte leads into it too.
    private bool LeadsInto(uint territoryId)
        => destinationTerritoryId == territoryId
        || (ZoneAetherytes.TryFindGateway(territoryId, out var gateway) && gateway.AetheryteId == destination.Id);

    private static bool LeadsToHuntZone(uint territoryId, uint aetheryteId)
    {
        if (HuntSpawns.HasZone(territoryId))
        {
            return true;
        }

        var zones = HuntSpawns.Zones;
        for (var index = 0; index < zones.Length; index++)
        {
            if (ZoneAetherytes.TryFindGateway(zones[index], out var gateway) && gateway.AetheryteId == aetheryteId)
            {
                return true;
            }
        }

        return false;
    }

    private string DescribeDestination() => $"{destination.Name} in {TerritoryNames.Of(destinationTerritoryId)}";

    private static string Describe(OfferCall call) => call switch
    {
        OfferCall.HoldAtFlag => "the ride is at its flag, waiting for the mark or fighting it",
        OfferCall.HoldOwnTeleport => "the ride is making its own way toward the flag",
        OfferCall.HoldBusy => $"a fight, a cast or a zone change is under way ({AutoCommon.ConditionTag()})",
        OfferCall.LeaveClosed => "the ride is no longer open to teleport offers",
        OfferCall.LeaveJourney => "the ride is still on its way to the train",
        OfferCall.LeaveOtherZone => $"it does not lead into {FlagZoneName()}, where the flag is",
        OfferCall.LeaveAlreadyThere => $"the character already stands in {FlagZoneName()}, where the flag is",
        OfferCall.LeaveNotHuntZone => "no flag is known yet, and it does not lead into a hunt zone",
        _ => "it serves the ride",
    };

    private static string FlagZoneName()
        => Plugin.Instance.Controller.TryGetFlagZone(out var territoryId) ? TerritoryNames.Of(territoryId) : "the flag's zone";

    private void EnsureTemplate()
    {
        if (templateLoaded)
        {
            return;
        }

        templateLoaded = true;
        try
        {
            offerPrompt = PromptTemplate.FromAddonRow(TeleportOfferPromptRow);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Teleport offer: failed to read the Addon sheet prompt template.");
        }

        if (!offerPrompt.IsValid)
        {
            RunLog.Warning("Teleport offer: the prompt template is unavailable; teleport offers cannot be identified.");
            return;
        }

        RunLog.Debug($"Teleport offer template: '{offerPrompt}'.");
    }
}
