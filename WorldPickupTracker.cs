namespace BetterLootTracker;

using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;
using OriathPlugins.Common.Inventory;
using OriathPlugins.Common.Loot;
using OriathPlugins.Common.Pricing;

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
        CurrencyDisplayNameStore displayNames,
        BetterLootTrackerSettings settings)
    {
        reportedPickups.Clear();
        var currentGroundItems = new Dictionary<uint, TrackedGroundItem>();

        foreach (var (_, entity) in area.AwakeEntities)
        {
            if (!TryDescribeGroundLoot(entity, displayNames, out var item))
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
            foreach (var pickup in TryCreatePickup(area, entity, displayNames, settings, "removed"))
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
        CurrencyDisplayNameStore displayNames,
        BetterLootTrackerSettings settings,
        string source)
    {
        if (!TryDescribeGroundLoot(entity, displayNames, out var item))
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
        CurrencyDisplayNameStore displayNames,
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

        if (!GroundLootRules.IsGroundLootEntity(entity))
        {
            return false;
        }

        var path = GroundLootRules.ResolveItemPath(entity) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !CurrencyPathMapper.IsInventoryLootPath(path))
        {
            return false;
        }

        var quantity = ItemStackCountReader.Read(entity);
        var displayName = GroundLootRules.ResolveDisplayName(entity);
        if (HostPriceHelper.TryGetDivineUnitValue(entity, out _, out var pricedName) &&
            !string.IsNullOrWhiteSpace(pricedName))
        {
            displayName = pricedName;
        }

        var priceId = PriceResolver.Resolve(path, displayName, displayNames) ??
                      CurrencyPathMapper.TryMapToNinjaId(path);
        displayName = PriceResolver.ResolveDisplayName(path, priceId, displayName, displayNames);

        item = new TrackedGroundItem
        {
            Path = path,
            PriceId = priceId,
            DisplayName = displayName,
            Quantity = quantity,
        };

        return true;
    }

    private struct TrackedGroundItem
    {
        public string Path;
        public string? PriceId;
        public string DisplayName;
        public int Quantity;
    }
}
