using AutoHuntTrain.Core.Travel;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoHuntTrain.Core.Train;

// One map flag heard in chat. World X and Z come straight from the link; the height is unknown until the zone's mesh
// is asked. Instance is 0 when neither the text nor the link named one, and InstanceFromLink says only the link did.
public readonly record struct FlagPost(
    DateTime PostedAtUtc,
    string SenderName,
    uint SenderWorldId,
    XivChatType ChatType,
    uint TerritoryId,
    uint MapId,
    float WorldX,
    float WorldZ,
    float MapX,
    float MapY,
    int Instance,
    bool InstanceFromLink,
    string Message)
{
    // Conductors post each flag twice within a moment, in Shout and in Yell for one; a copy lands within half a map
    // unit of the first, and a train never flags the same spot again inside a minute and a half.
    private const float RepeatMapTolerance = 0.5f;
    private static readonly TimeSpan RepeatWindow = TimeSpan.FromSeconds(90);

    public bool NamesInstance => Instance > 0;

    public bool NamesInstanceInText => NamesInstance && !InstanceFromLink;

    public bool Repeats(in FlagPost earlier)
        => TerritoryId == earlier.TerritoryId
        && Instance == earlier.Instance
        && MathF.Abs(MapX - earlier.MapX) <= RepeatMapTolerance
        && MathF.Abs(MapY - earlier.MapY) <= RepeatMapTolerance
        && (PostedAtUtc - earlier.PostedAtUtc).Duration() <= RepeatWindow;
}

// A chat line with no map link, heard only while someone listens for them.
public readonly record struct SpokenLine(string SenderName, uint SenderWorldId, XivChatType ChatType, string Text);

// Hears every map flag posted in the hunt channels on the current world and keeps the last few, so a conductor can
// be picked from the players who post flags and a ride can pick up the flag posted just before it started. Chat
// arrives on the framework thread, and so does every reader.
internal sealed class FlagListener : IDisposable
{
    public const int Capacity = 32;

    private const uint OpenWorldIntendedUse = 1;
    // A raw link coordinate is the world coordinate times this.
    private const float RawCoordinateScale = 1000f;
    // Older chat APIs packed the source and target into the high bits of the kind; masking them off costs nothing.
    private const ushort ChatKindMask = 0x7F;

    // Hub zones with no open-world use where trains are still called and flagged.
    private static readonly uint[] hubTerritoryIds = [1024, 682, 739, 759, 635, 659, 478];

    private readonly FlagPost[] posts = new FlagPost[Capacity];
    private readonly IChatGui.OnHandleableChatMessageDelegate onChatMessage;
    private int start;
    private int count;
    private int version;
    private uint checkedTerritoryId = uint.MaxValue;
    private bool checkedTerritoryIsHunt;

    public event Action<FlagPost>? Posted;

    // Lines without a map link in Shout, Yell, Say and party chat, from any zone.
    public event Action<SpokenLine>? Spoken;

    public FlagListener()
    {
        onChatMessage = OnChatMessage;
        Svc.Chat.ChatMessage += onChatMessage;
    }

    public void Dispose() => Svc.Chat.ChatMessage -= onChatMessage;

    public int Count => count;

    // Bumps on every post, so a view rebuilds what it derives from the ring only when the ring changed.
    public int Version => version;

    public FlagPost FromNewest(int index) => posts[(start + count - 1 - index) % Capacity];

    public bool TryLatestBy(in ConductorIdentity sender, out FlagPost post)
    {
        for (var index = 0; index < count; index++)
        {
            var candidate = FromNewest(index);
            if (!sender.Matches(candidate))
            {
                continue;
            }

            post = candidate;
            return true;
        }

        post = default;
        return false;
    }

    // Every distinct poster, newest first; the destination should hold Capacity entries.
    public int RecentSenders(Span<ConductorIdentity> destination)
    {
        var written = 0;
        for (var index = 0; index < count && written < destination.Length; index++)
        {
            var post = FromNewest(index);
            var identity = new ConductorIdentity(post.SenderName, post.SenderWorldId);
            if (Contains(destination[..written], identity))
            {
                continue;
            }

            destination[written++] = identity;
        }

        return written;
    }

    private static bool Contains(ReadOnlySpan<ConductorIdentity> identities, in ConductorIdentity identity)
    {
        for (var index = 0; index < identities.Length; index++)
        {
            if (identities[index].SameAs(identity))
            {
                return true;
            }
        }

        return false;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        var chatType = (XivChatType)((ushort)message.LogKind & ChatKindMask);
        var listening = Listening(chatType) && InHuntTerritory();
        var hearingSpoken = Spoken is not null && SpokenChannel(chatType);
        if (!listening && !hearingSpoken)
        {
            return;
        }

        if (FirstMapLink(message.Message) is not { } link)
        {
            if (hearingSpoken)
            {
                RelaySpoken(message, chatType);
            }

            return;
        }

        if (!listening || !TryReadSender(message.Sender, out var name, out var worldId))
        {
            return;
        }

        var text = message.Message.TextValue;
        var territoryId = link.TerritoryType.RowId;
        var hint = InstanceHints.Parse(text);
        var instance = ResolveInstance(hint, name, worldId, territoryId, out var fromLink);
        var post = new FlagPost(
            DateTime.UtcNow, name, worldId, chatType,
            territoryId, link.Map.RowId,
            link.RawX / RawCoordinateScale, link.RawY / RawCoordinateScale,
            link.XCoord, link.YCoord, instance, fromLink, text);
        Push(post);
        RunLog.Debug($"Flag heard: {name}@{worldId} in {TerritoryNames.Of(post.TerritoryId)} at ({post.MapX:F1}, {post.MapY:F1}), instance {instance} ({InstanceSourceText(hint, instance, fromLink)}), via {chatType}");
        Posted?.Invoke(post);
    }

    // Text naming the instance wins, then "same" for the one the poster last named. The link's glyph is only where the
    // poster stood, so it comes last, and only for a flag in the zone the listener is in: Shout, Yell and Say never
    // cross zones, so a flag for another zone was posted by someone not standing in it.
    private int ResolveInstance(in InstanceHint hint, string name, uint worldId, uint territoryId, out bool fromLink)
    {
        fromLink = false;
        if (hint.NamesNumber)
        {
            return hint.Number;
        }

        if (hint.Same && LastInstanceNamedBy(name, worldId) is > 0 and var last)
        {
            return last;
        }

        if (hint.Linked == 0 || territoryId != Svc.ClientState.TerritoryType)
        {
            return 0;
        }

        fromLink = true;
        return hint.Linked;
    }

    private static string InstanceSourceText(in InstanceHint hint, int instance, bool fromLink)
    {
        if (fromLink)
        {
            return "from the map link";
        }

        if (instance == 0)
        {
            return hint.Linked > 0 ? $"the link's instance {hint.Linked} ignored for a flag in another zone" : "none named";
        }

        return hint.Linked > 0 && hint.Linked != instance ? $"from the text, over the link's instance {hint.Linked}" : "from the text";
    }

    private void RelaySpoken(IHandleableChatMessage message, XivChatType chatType)
    {
        if (!TryReadSender(message.Sender, out var name, out var worldId))
        {
            return;
        }

        Spoken?.Invoke(new SpokenLine(name, worldId, chatType, message.Message.TextValue));
    }

    private static bool SpokenChannel(XivChatType chatType)
        => chatType is XivChatType.Shout or XivChatType.Yell or XivChatType.Say or XivChatType.Party or XivChatType.CrossParty;

    private static bool Listening(XivChatType chatType)
    {
        var configuration = Plugin.Instance.Configuration;
        return chatType switch
        {
            XivChatType.Shout => configuration.ListenShout,
            XivChatType.Yell => configuration.ListenYell,
            XivChatType.Say => configuration.ListenSay,
            _ => false,
        };
    }

    private bool InHuntTerritory()
    {
        var territoryId = Svc.ClientState.TerritoryType;
        if (territoryId != checkedTerritoryId)
        {
            checkedTerritoryId = territoryId;
            checkedTerritoryIsHunt = IsHuntTerritory(territoryId);
        }

        return checkedTerritoryIsHunt;
    }

    private static bool IsHuntTerritory(uint territoryId)
        => Array.IndexOf(hubTerritoryIds, territoryId) >= 0
        || Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.TerritoryIntendedUse.RowId == OpenWorldIntendedUse;

    private static MapLinkPayload? FirstMapLink(SeString message)
    {
        var payloads = message.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is MapLinkPayload link)
            {
                return link;
            }
        }

        return null;
    }

    // The game links every other player's name; the local player's own lines carry the name as plain text.
    private static bool TryReadSender(SeString sender, out string name, out uint worldId)
    {
        var payloads = sender.Payloads;
        for (var index = 0; index < payloads.Count; index++)
        {
            if (payloads[index] is not PlayerPayload player)
            {
                continue;
            }

            name = player.PlayerName;
            worldId = player.World.RowId;
            return true;
        }

        if (Svc.Objects.LocalPlayer is { } local)
        {
            var localName = local.Name.TextValue;
            if (string.Equals(sender.TextValue.Trim(), localName, StringComparison.Ordinal))
            {
                name = localName;
                worldId = local.HomeWorld.RowId;
                return true;
            }
        }

        name = string.Empty;
        worldId = 0;
        return false;
    }

    // "same" keeps the instance the same poster last named, so a flag that says only that still has one.
    private int LastInstanceNamedBy(string name, uint worldId)
    {
        for (var index = 0; index < count; index++)
        {
            var post = FromNewest(index);
            if (post.SenderWorldId == worldId && string.Equals(post.SenderName, name, StringComparison.Ordinal))
            {
                return post.Instance;
            }
        }

        return 0;
    }

    private void Push(in FlagPost post)
    {
        posts[(start + count) % Capacity] = post;
        if (count < Capacity)
        {
            count++;
        }
        else
        {
            start = (start + 1) % Capacity;
        }

        version++;
    }
}
