using FFXIVClientStructs.FFXIV.Component.GUI;

namespace AutoHuntTrain.Core.Game.Watchers;

// The yes/no prompts a party member raises: an invite into their party and an offer to teleport the character along.
// Only the watchers made for them answer them, each by its own rules, and anything that clicks through other dialogs
// leaves them alone. Both are matched against their Addon sheet templates, so the check works in every client language.
internal static unsafe class PartyPrompts
{
    private const uint JoinPartyRow = 120;
    private const uint TeleportOfferRow = 166;

    private static PromptTemplate joinParty = PromptTemplate.Invalid;
    private static PromptTemplate teleportOffer = PromptTemplate.Invalid;
    private static bool loaded;

    public static PromptTemplate JoinParty
    {
        get
        {
            EnsureLoaded();
            return joinParty;
        }
    }

    public static PromptTemplate TeleportOffer
    {
        get
        {
            EnsureLoaded();
            return teleportOffer;
        }
    }

    public static bool Matches(AtkUnitBase* addon)
    {
        var prompt = SelectYesnoPrompt.Read(addon);
        return JoinParty.Matches(prompt) || TeleportOffer.Matches(prompt);
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        joinParty = Load(JoinPartyRow, "party invite");
        teleportOffer = Load(TeleportOfferRow, "teleport offer");
    }

    private static PromptTemplate Load(uint rowId, string kind)
    {
        var template = PromptTemplate.Invalid;
        try
        {
            template = PromptTemplate.FromAddonRow(rowId);
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, $"Prompts: failed to read the {kind} template from the Addon sheet.");
        }

        if (!template.IsValid)
        {
            RunLog.Warning($"Prompts: the {kind} template is unavailable; such prompts cannot be identified.");
            return template;
        }

        RunLog.Debug($"Prompts: the {kind} template is '{template}'.");
        return template;
    }
}
