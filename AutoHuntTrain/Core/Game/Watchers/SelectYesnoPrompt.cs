using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

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
            var text = master.Addon->PromptText is not null ? master.Text : master.TextLegacy;
            return PromptTemplate.Normalize(text).Trim();
        }
        catch (Exception exception)
        {
            RunLog.Debug($"SelectYesno {addon->Id}: failed to read the prompt: {exception.Message}");
            return "";
        }
    }
}
