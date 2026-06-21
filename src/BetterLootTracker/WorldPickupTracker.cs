namespace BetterLootTracker;

using OriathHub.RemoteEnums;
using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;

internal readonly record struct CurrencyPickup(
    string ItemPath,
    string? PriceId,
    string DisplayName,
    int Quantity);

internal sealed class WorldPickupTracker
{
    private readonly Dictionary<uint, TrackedGroundItem> trackedGroundItems = new();
    private readonly HashSet<uint> reportedPickups = new();
    private int groundLootSeenFrames;

    public void Reset()
    {
        trackedGroundItems.Clear();
        reportedPickups.Clear();
        groundLootSeenFrames = 0;
    }

    public IEnumerable<CurrencyPickup> ProcessFrame(
        AreaInstance area,
        NinjaPriceCatalog priceCatalog,
        BetterLootTrackerSettings settings)
    {
        reportedPickups.Clear();
        var currentGroundItems = new Dictionary<uint, TrackedGroundItem>();

        foreach (var (_, entity) in area.AwakeEntities)
        {
            if (!TryDescribeGroundLoot(entity, priceCatalog, out var item))
            {
                continue;
            }

            groundLootSeenFrames++;
            if (settings.EnableDebugLogging && groundLootSeenFrames <= 5)
            {
                Log.Info(
                    $"ground loot visible: type={entity.EntityType} subtype={entity.EntitySubtype} path={entity.Path}",
                    "Better Loot Tracker");
            }

            if (!IsNearPlayer(area, entity, settings.PickupDistance))
            {
                continue;
            }

            currentGroundItems[entity.Id] = item;
        }

        foreach (var entity in area.EntitiesRemovedThisFrame)
        {
            foreach (var pickup in TryCreatePickup(area, entity, priceCatalog, settings, "removed"))
            {
                yield return pickup;
            }
        }

        foreach (var (entityId, previous) in trackedGroundItems)
        {
            if (currentGroundItems.ContainsKey(entityId))
            {
                continue;
            }

            if (!reportedPickups.Add(entityId))
            {
                continue;
            }

            if (!CurrencyFilter.ShouldTrack(settings, previous.PriceId, previous.Path))
            {
                continue;
            }

            if (settings.EnableDebugLogging)
            {
                Log.Info(
                    $"pickup (vanished): {previous.Quantity}x {previous.DisplayName} ({previous.Path})",
                    "Better Loot Tracker");
            }

            yield return new CurrencyPickup(
                previous.Path,
                previous.PriceId,
                previous.DisplayName,
                previous.Quantity);
        }

        trackedGroundItems.Clear();
        foreach (var (entityId, item) in currentGroundItems)
        {
            trackedGroundItems[entityId] = item;
        }
    }

    private IEnumerable<CurrencyPickup> TryCreatePickup(
        AreaInstance area,
        Entity entity,
        NinjaPriceCatalog priceCatalog,
        BetterLootTrackerSettings settings,
        string source)
    {
        if (!TryDescribeGroundLoot(entity, priceCatalog, out var item))
        {
            yield break;
        }

        if (!IsNearPlayer(area, entity, settings.PickupDistance))
        {
            yield break;
        }

        if (!reportedPickups.Add(entity.Id))
        {
            yield break;
        }

        if (!CurrencyFilter.ShouldTrack(settings, item.PriceId, item.Path))
        {
            yield break;
        }

        if (settings.EnableDebugLogging)
        {
            Log.Info(
                $"pickup ({source}): {item.Quantity}x {item.DisplayName} ({item.Path})",
                "Better Loot Tracker");
        }

        yield return new CurrencyPickup(item.Path, item.PriceId, item.DisplayName, item.Quantity);
    }

    private static bool IsNearPlayer(AreaInstance area, Entity entity, float pickupDistance)
    {
        if (!entity.IsValid)
        {
            return false;
        }

        if (entity.Zones is NearbyZones.InnerCircle or NearbyZones.OuterCircle)
        {
            return true;
        }

        var player = area.Player;
        if (!player.IsValid)
        {
            return false;
        }

        var distance = entity.DistanceFrom(player);
        return distance > 0 && distance <= pickupDistance;
    }

    private static bool TryDescribeGroundLoot(
        Entity entity,
        NinjaPriceCatalog priceCatalog,
        out TrackedGroundItem item)
    {
        item = default;
        if (!entity.IsValid)
        {
            return false;
        }

        if (entity.EntityType is EntityTypes.Monster or EntityTypes.Player or EntityTypes.NPC)
        {
            return false;
        }

        if (!IsLikelyGroundLootEntity(entity))
        {
            return false;
        }

        var path = entity.Path;
        if (!CurrencyPathMapper.IsGroundLootPath(path, priceCatalog))
        {
            return false;
        }

        var quantity = ItemStackCountReader.Read(entity.Address);
        if (entity.TryGetComponent<Stack>(out var stack) && stack.Count > quantity)
        {
            quantity = stack.Count;
        }

        var priceId = CurrencyPathMapper.TryMapToNinjaId(path, priceCatalog);
        var displayName = CurrencyPathMapper.GetDisplayName(path, priceCatalog.TryGetDisplayName(priceId));

        item = new TrackedGroundItem
        {
            Path = path,
            PriceId = priceId,
            DisplayName = displayName,
            Quantity = quantity,
        };

        return true;
    }

    private static bool IsLikelyGroundLootEntity(Entity entity) =>
        entity.EntityType is EntityTypes.Item ||
        entity.EntitySubtype is EntitySubtypes.WorldItem ||
        entity.EntityType is EntityTypes.Unidentified;

    private struct TrackedGroundItem
    {
        public string Path;
        public string? PriceId;
        public string DisplayName;
        public int Quantity;
    }
}
