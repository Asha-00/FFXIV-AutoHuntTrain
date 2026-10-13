using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// Turns HuntAlerts' train and S-rank announcements into hunts waiting to be ridden. Relay fields are untrusted, so an
// announcement carries only what checked out. Alerts may arrive off the game thread; every one is moved onto it before
// it touches the ring.
internal sealed class FeedListener : IDisposable
{
    // Every region's announcements are kept; eviction favours the player's own region and allowed data centers.
    public const int Capacity = 48;

    private const string EventName = "HuntAlerts.OnHuntAlertMessageReceived";
    private const string TrainType = "new_hunt";
    internal const string RankSType = "srank";
    private const int MaxInstance = 9;
    private const int TickIntervalMs = 1_000;

    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromMinutes(2);
    // A start further away than this is a mangled tag or a relay clock gone wrong, not a train.
    private static readonly TimeSpan MaxLead = TimeSpan.FromHours(6);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(6);

    private readonly ICallGateSubscriber<HuntAlertMessage, object> subscriber;
    private readonly Action<HuntAlertMessage> onMessage;
    private readonly Announcement[] items = new Announcement[Capacity];
    private int count;
    private int version;
    // Chat links carry the id, and a reload restarts the count; seeding from the clock keeps a link printed before the
    // reload from opening another train after it. A thousand ids per second of seed leaves room for any burst.
    private int nextId = (int)(DateTimeOffset.UtcNow.ToUnixTimeSeconds() % 1_000_000) * 1_000 + 1;
    private bool feedLoaded;
    private long nextTickAtMs;
    private int savedVersion;

    public event Action<Announcement>? Announced;

    public FeedListener()
    {
        onMessage = OnMessage;
        subscriber = Svc.PluginInterface.GetIpcSubscriber<HuntAlertMessage, object>(EventName);
        subscriber.Subscribe(onMessage);
        feedLoaded = ExternalPlugins.IsInstalled(ExternalPlugin.HuntAlerts);
        RestoreSaved();
    }

    public void Dispose()
    {
        subscriber.Unsubscribe(onMessage);
        SaveIfChanged();
    }

    // Announcements saved before a reload come back without notifying again; expired entries stay gone.
    private void RestoreSaved()
    {
        var saved = FeedStore.Load();
        var nowUtc = DateTime.UtcNow;
        var restored = 0;
        for (var index = 0; index < saved.Length; index++)
        {
            if (saved[index] is not { } stored || !stored.TryRebuild(nextId, out var announcement))
            {
                continue;
            }

            if (IsExpired(announcement, nowUtc))
            {
                continue;
            }

            if (Insert(announcement))
            {
                nextId++;
                restored++;
            }
        }

        savedVersion = version;
        if (restored > 0)
        {
            RunLog.Info($"Feed: restored {restored} announcement(s) saved before the reload");
        }
    }

    private void SaveIfChanged()
    {
        if (version == savedVersion)
        {
            return;
        }

        savedVersion = version;
        FeedStore.Save(items.AsSpan(0, count));
    }

    public int Count => count;

    // The id the next announcement will get; every item already in the ring has a lower one.
    public int NextId => nextId;

    // Bumps on every change, so a view rebuilds what it derives from the ring only when the ring changed.
    public int Version => version;

    public bool IsFeedLoaded => feedLoaded;

    // Trains retain their start-time ordering; S ranks use their report time as their compatible sort timestamp.
    public Announcement this[int index] => items[index];

    public bool TryFind(int id, out Announcement announcement)
    {
        for (var index = 0; index < count; index++)
        {
            if (items[index].Id != id)
            {
                continue;
            }

            announcement = items[index];
            return true;
        }

        announcement = default;
        return false;
    }

    // Once a second: the plugin list is re-read, and expired trains and S-rank relays leave the ring.
    public void Tick()
    {
        var now = Environment.TickCount64;
        if (now < nextTickAtMs)
        {
            return;
        }

        nextTickAtMs = now + TickIntervalMs;
        feedLoaded = ExternalPlugins.IsInstalled(ExternalPlugin.HuntAlerts);
        Prune(DateTime.UtcNow);
        SaveIfChanged();
    }

    // The one intake for the IPC event and the debug injection; framework thread only.
    public void Accept(in HuntAlertMessage message)
    {
        var type = message.HuntType ?? string.Empty;
        if (string.Equals(type, RankSType, StringComparison.OrdinalIgnoreCase))
        {
            var receivedAtUtc = DateTime.UtcNow;
            var id = nextId;
            if (!SRankAnnouncementBuilder.TryBuild(message, id, receivedAtUtc, out var sRank, out var sRankProblem))
            {
                RunLog.Warning($"Feed: dropped an S-rank announcement, {sRankProblem}");
                return;
            }
            Prune(sRank.ReceivedAtUtc);

            if (TryMergeSRankDuplicate(sRank))
            {
                return;
            }

            nextId++;
            if (!Insert(sRank))
            {
                return;
            }

            RunLog.Info(Describe(sRank));
            Announced?.Invoke(sRank);
            return;
        }

        if (!string.Equals(type, TrainType, StringComparison.OrdinalIgnoreCase))
        {
            RunLog.Debug($"Feed: ignoring an alert of type '{type}'");
            return;
        }

        if (!TryBuild(message, out var announcement, out var problem))
        {
            RunLog.Warning($"Feed: dropped a train announcement, {problem}");
            return;
        }

        if (TryMergeDuplicate(announcement))
        {
            return;
        }

        Prune(announcement.ReceivedAtUtc);
        if (!Insert(announcement))
        {
            return;
        }

        RunLog.Info(Describe(announcement));
        Announced?.Invoke(announcement);
    }

    private void OnMessage(HuntAlertMessage message)
        => _ = Svc.Framework.RunOnFrameworkThread(() => Accept(message));

    private bool TryBuild(in HuntAlertMessage message, out Announcement announcement, out string problem)
    {
        announcement = default;
        problem = string.Empty;
        var now = DateTime.UtcNow;
        var text = AnnouncementText.StripEmoji(message.Message ?? string.Empty);
        if (!ExpansionGroups.TryParse(message.HuntKind ?? string.Empty, out var group))
        {
            problem = $"unknown hunt kind '{message.HuntKind}'";
            return false;
        }

        if (!Worlds.TryFindById(message.HuntWorldId, out var world))
        {
            problem = $"unknown world id {message.HuntWorldId}";
            return false;
        }

        var postedAt = PostedAt(message, now);
        DateTime startAt;
        if (AnnouncementText.TryReadStartTime(text, out var announced))
        {
            startAt = announced;
            RunLog.Debug($"Feed: start time for {world.Name} read from a Discord timestamp");
        }
        else if (AnnouncementText.TryReadClockTime(text, postedAt, out var clock))
        {
            startAt = clock;
            RunLog.Debug($"Feed: start time for {world.Name} read from the clock time in the post");
        }
        else
        {
            startAt = postedAt;
            RunLog.Info($"Feed: no start time found in the post for {world.Name}; the post time stands in. Text: {Flatten(text)}");
        }
        if (startAt - now > MaxLead || now - startAt > MaxAge)
        {
            problem = $"the start time {startAt:yyyy-MM-dd HH:mm:ss}Z is out of range";
            return false;
        }

        ResolvePlace(message, out var aetheryteId, out var territoryId);
        var instance = message.Instance is > 0 and <= MaxInstance ? message.Instance : 0;
        Vector2? coordinates = message.MapLocationCoords is { } point && float.IsFinite(point.X) && float.IsFinite(point.Y) ? point : null;
        announcement = new Announcement(nextId++, now, postedAt, world, group, startAt, aetheryteId, territoryId, instance, coordinates, ReadConductor(text, world), text);
        return true;
    }

    private static DateTime PostedAt(in HuntAlertMessage message, DateTime now)
    {
        if (message.PostedEpoch > 0)
        {
            var epoch = message.PostedEpoch;
            if (epoch > DateTimeOffset.MaxValue.ToUnixTimeSeconds())
            {
                return now;
            }

            return DateTimeOffset.FromUnixTimeSeconds(epoch).UtcDateTime;
        }

        var posted = message.PostedTime.UtcDateTime;
        return posted.Year > 2000 ? posted : now;
    }

    // The aetheryte is the more specific fact; a territory that disagrees with it is the relay's mistake.
    private static void ResolvePlace(in HuntAlertMessage message, out uint aetheryteId, out uint territoryId)
    {
        aetheryteId = message.StartingAetheryteId;
        territoryId = message.StartingTerritoryTypeId;
        if (aetheryteId != 0)
        {
            if (ZoneAetherytes.TryFindById(aetheryteId, out var aetheryteTerritoryId, out _))
            {
                if (territoryId != 0 && territoryId != aetheryteTerritoryId)
                {
                    RunLog.Debug($"Feed: territory {territoryId} disagrees with aetheryte {aetheryteId} in territory {aetheryteTerritoryId}; the aetheryte wins");
                }

                territoryId = aetheryteTerritoryId;
            }
            else
            {
                RunLog.Debug($"Feed: aetheryte {aetheryteId} is not a teleport target; the announcement keeps only its territory");
                aetheryteId = 0;
            }
        }

        if (territoryId != 0 && Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId) is null)
        {
            RunLog.Debug($"Feed: territory {territoryId} is not one this client knows; the announcement names no zone");
            territoryId = 0;
        }
    }

    // A conductor named without a world, or with one the client does not know, is taken to be from the train's world.
    private static ConductorIdentity ReadConductor(string text, in WorldInfo trainWorld)
    {
        if (!AnnouncementText.TryReadConductor(text, out var name, out var worldName))
        {
            return ConductorIdentity.None;
        }

        var world = trainWorld;
        if (worldName.Length > 0 && !Worlds.TryFind(worldName, out world))
        {
            world = trainWorld;
        }

        return new ConductorIdentity(name, world.Id);
    }

    // A repeated relay for the same S rank, world, territory and instance adds no useful information.
    // The live actor supplies the current position after arrival, so retain the original row and expiry.
    private bool TryMergeSRankDuplicate(
        in Announcement announcement)
    {
        for (var index = 0; index < count; index++)
        {
            var existing = items[index];
            if (!SRankFeedPolicy.SameTarget(
                    existing,
                    announcement))
            {
                continue;
            }

            RunLog.Debug(
                $"Feed: repeated S-rank relay on {announcement.World.Name} was ignored");
            return true;
        }

        return false;
    }

    // The same train announced again fills in what the first announcement lacked and is otherwise dropped.
    private bool TryMergeDuplicate(in Announcement announcement)
    {
        for (var index = 0; index < count; index++)
        {
            var existing = items[index];
            if (!existing.SameTrainAs(announcement, DuplicateWindow))
            {
                continue;
            }

            var merged = existing with
            {
                Conductor = existing.NamesConductor ? existing.Conductor : announcement.Conductor,
                AetheryteId = existing.NamesAetheryte ? existing.AetheryteId : announcement.AetheryteId,
                TerritoryId = existing.NamesTerritory ? existing.TerritoryId : announcement.TerritoryId,
                Instance = existing.NamesInstance ? existing.Instance : announcement.Instance,
                MapCoordinates = existing.MapCoordinates ?? announcement.MapCoordinates,
            };
            if (merged == existing)
            {
                RunLog.Debug($"Feed: the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} was announced again; dropped as a duplicate");
                return true;
            }

            items[index] = merged;
            version++;
            RunLog.Info($"Feed: the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} was announced again and its details were filled in: {Describe(merged)}");
            return true;
        }

        return false;
    }

    // Kept in start order. A full ring lets go of the train that matters least to the player: one in another region
    // before one on a data center they do not ride, and the one starting last among equals.
    private bool Insert(in Announcement announcement)
    {
        if (count == Capacity)
        {
            var victim = EvictionCandidate();
            if (!Outranks(announcement, items[victim]))
            {
                RunLog.Debug($"Feed: {Capacity} trains are already kept; the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} matters least and is not kept");
                return false;
            }

            RunLog.Debug($"Feed: {Capacity} trains are already kept; the {ExpansionGroups.Name(items[victim].Group)} train on {items[victim].World.Name} matters least and makes room");
            RemoveAt(victim);
        }

        var slot = count;
        while (slot > 0 && items[slot - 1].StartAtUtc > announcement.StartAtUtc)
        {
            items[slot] = items[slot - 1];
            slot--;
        }

        items[slot] = announcement;
        count++;
        version++;
        return true;
    }

    private int EvictionCandidate()
    {
        var candidate = count - 1;
        var candidatePriority = KeepPriorityOf(items[candidate]);
        for (var index = count - 2; index >= 0 && candidatePriority > KeepPriority.OtherRegion; index--)
        {
            var priority = KeepPriorityOf(items[index]);
            if (priority >= candidatePriority)
            {
                continue;
            }

            candidate = index;
            candidatePriority = priority;
        }

        return candidate;
    }

    private static bool Outranks(in Announcement incoming, in Announcement kept)
    {
        var incomingPriority = KeepPriorityOf(incoming);
        var keptPriority = KeepPriorityOf(kept);
        return incomingPriority > keptPriority || (incomingPriority == keptPriority && incoming.StartAtUtc < kept.StartAtUtc);
    }

    // With no character logged in there is no home to measure against, and every train ranks the same.
    private static KeepPriority KeepPriorityOf(in Announcement announcement)
    {
        if (!Worlds.TryHome(out var home))
        {
            return KeepPriority.AllowedDataCenter;
        }

        if (home.Region != announcement.World.Region)
        {
            return KeepPriority.OtherRegion;
        }

        return RideRules.IsAllowedDataCenter(announcement.World) ? KeepPriority.AllowedDataCenter : KeepPriority.OtherDataCenter;
    }

    private void RemoveAt(int index)
    {
        for (var slot = index; slot < count - 1; slot++)
        {
            items[slot] = items[slot + 1];
        }

        count--;
        items[count] = default;
        version++;
    }

    private void Prune(DateTime nowUtc)
    {
        var kept = 0;
        for (var index = 0; index < count; index++)
        {
            var item = items[index];
            if (!IsExpired(item, nowUtc))
            {
                items[kept++] = item;
                continue;
            }

            if (item.IsSRank)
            {
                var creature = item.CreatureNameOr("an unknown S rank");
                RunLog.Debug($"Feed: {creature} on {item.World.Name} was reported {(nowUtc - item.PostedAtUtc).TotalMinutes:F0} minutes ago and leaves the list");
            }
            else
            {
                RunLog.Debug($"Feed: the {ExpansionGroups.Name(item.Group)} train on {item.World.Name} started {(nowUtc - item.StartAtUtc).TotalMinutes:F0} minutes ago and leaves the list");
            }
        }

        if (kept == count)
        {
            return;
        }

        for (var index = kept; index < count; index++)
        {
            items[index] = default;
        }

        count = kept;
        version++;
    }

    private static bool IsExpired(in Announcement announcement, DateTime nowUtc)
        => announcement.IsSRank
            ? SRankFeedPolicy.IsExpired(announcement, nowUtc)
            : announcement.StartAtUtc < nowUtc - Announcement.ListedAfterStart;

    private enum KeepPriority : byte
    {
        OtherRegion,
        OtherDataCenter,
        AllowedDataCenter,
    }

    // One log line for a post of several lines, cut short so a long post cannot flood the console.
    private static string Flatten(string text)
    {
        const int MaxLogLength = 600;
        var flat = text.Replace("\r", string.Empty, StringComparison.Ordinal).Replace("\n", " | ", StringComparison.Ordinal);
        return flat.Length <= MaxLogLength ? flat : string.Concat(flat.AsSpan(0, MaxLogLength), "...");
    }

    private static string Describe(in Announcement announcement)
    {
        var place = announcement.NamesTerritory ? TerritoryNames.Of(announcement.TerritoryId) : "an unknown zone";
        var stop = announcement.NamesAetheryte && ZoneAetherytes.TryFindById(announcement.AetheryteId, out _, out var aetheryte) ? $" at {aetheryte.Name}" : string.Empty;
        var instance = announcement.NamesInstance ? $" instance {announcement.Instance}" : string.Empty;

        if (announcement.IsSRank)
        {
            var creature = announcement.CreatureNameOr("an unknown S rank");
            var coordinates = announcement.MapCoordinates is { } point
                ? $" at map ({point.X:F1}, {point.Y:F1})"
                : string.Empty;
            return $"Feed: S rank {creature} on {announcement.World.Name} ({announcement.World.DataCenterName}), reported {announcement.PostedAtUtc:HH:mm}Z in {place}{stop}{instance}{coordinates}";
        }

        var lead = announcement.LeadAt(DateTime.UtcNow).TotalMinutes;
        var conductor = announcement.NamesConductor ? $", conductor {Conductor.Describe(announcement.Conductor)}" : ", no conductor named";
        return $"Feed: {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} ({announcement.World.DataCenterName}) starting {announcement.StartAtUtc:HH:mm}Z ({lead:F0} min from now) in {place}{stop}{instance}{conductor}";
    }
}
