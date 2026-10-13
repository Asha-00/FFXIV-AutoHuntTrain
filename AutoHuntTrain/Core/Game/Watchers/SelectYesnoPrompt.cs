using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System.Text;

namespace AutoHuntTrain.Core.Game.Watchers;

// The game's yes/no prompt. A watcher keeps the id of the one it answers and looks the addon up again right before the
// click, since the one a setup event handed over can be gone or replaced by then.
internal static unsafe class SelectYesnoPrompt
{
    public const string AddonName = "SelectYesno";

    public static AtkUnitBase* Find(ushort addonId)
    {
        var addon = RaptureAtkUnitManager.Instance()->GetAddonById(addonId);
        if (addon is null)
        {
            return null;
        }

        return addon->NameString == AddonName ? addon : null;
    }

    public static string Read(AtkUnitBase* addon)
    {
        try
        {
            var master = new AddonMaster.SelectYesno((nint)addon);
            var text = master.Addon->PromptText is not null ? TextOf(master.SeString) : master.TextLegacy;
            return PromptTemplate.Normalize(text).Trim();
        }
        catch (Exception exception)
        {
            RunLog.Debug($"SelectYesno {addon->Id}: failed to read the prompt: {exception.Message}");
            return "";
        }
    }

    // The text payloads alone, so a layout macro reads as nothing, the way PromptTemplate drops it: ECommons reads a
    // non-breaking space as a plain space, and French "Destination<nbsp>:" would then never match its template.
    private static string TextOf(SeString prompt)
    {
        var payloads = prompt.Payloads;
        var builder = new StringBuilder();
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is TextPayload { Text: { } text })
            {
                builder.Append(text);
            }
        }

        return builder.ToString();
    }
}
