namespace OriathPlugins.Common.Loot;

using OriathHub.RemoteEnums;
using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;
using OriathPlugins.Common.Pricing;

public static class GroundLootRules
{
    private const string WorldItemPlaceholderMarker = "MiscellaneousObjects/WorldItem";

    private static readonly string[] NonLootMiscellaneousMarkers =
    [
        "MiscellaneousObjects/Checkpoints/",
        "MiscellaneousObjects/Doodad",
        "MiscellaneousObjects/Expedition",
        "MiscellaneousObjects/Brequel",
        "MiscellaneousObjects/AreaTransition",
    ];

    private static readonly string[] WorldObjectPathMarkers =
    [
        "MultiplexPortal",
        "AreaTransition",
        "TownPortal",
        "MapPortal",
        "/Portals/",
        "/PortalObject",
        "/Transition/",
        "MappingDevice",
        "LabyrinthEntry",
    ];

    public static bool IsGroundLootEntity(Entity entity)
    {
        if (!entity.IsValid)
        {
            return false;
        }

        if (entity.EntitySubtype is EntitySubtypes.InventoryItem)
        {
            return false;
        }

        if (entity.EntityState is EntityStates.Useless && !IsWorldItemPlaceholder(entity))
        {
            return false;
        }

        if (IsExcludedWorldObject(entity))
        {
            return false;
        }

        if (IsWorldItemPlaceholder(entity))
        {
            return true;
        }

        if (GroundItemPath.IsLootPath(entity.Path))
        {
            return true;
        }

        if (entity.EntitySubtype is EntitySubtypes.WorldItem)
        {
            return true;
        }

        if (entity.EntityType is EntityTypes.Item &&
            HasDroppableItemComponents(entity))
        {
            return true;
        }

        return false;
    }

    public static bool ShouldPickup(Entity entity, bool currencyOnly)
    {
        if (!IsGroundLootEntity(entity))
        {
            return false;
        }

        if (!currencyOnly)
        {
            return true;
        }

        return LootPathMatcher.IsCurrencyPickup(entity, null, alwaysPickupWaystonesAndTablets: true);
    }

    public static IEnumerable<string> EnumerateItemPathCandidates(Entity entity)
    {
        if (!string.IsNullOrWhiteSpace(entity.Path))
        {
            yield return entity.Path;
        }

        if (entity.TryGetComponent<Animated>(out var animated) &&
            !string.IsNullOrWhiteSpace(animated.Path))
        {
            yield return animated.Path;
        }

        if (entity.TryGetComponent<RenderItem>(out var render) &&
            !string.IsNullOrWhiteSpace(render.ResourcePath))
        {
            yield return render.ResourcePath;
        }
    }

    public static string ResolveDisplayName(Entity entity)
    {
        var itemPath = ResolveItemPath(entity);
        if (!string.IsNullOrWhiteSpace(itemPath) &&
            !itemPath.Contains(WorldItemPlaceholderMarker, StringComparison.OrdinalIgnoreCase))
        {
            return LootPathMatcher.GetDisplayName(itemPath);
        }

        if (entity.TryGetComponent<ObjectMagicProperties>(out var magic))
        {
            return $"Loot ({magic.Rarity})";
        }

        return "Ground loot";
    }

    public static string? ResolveItemPath(Entity entity)
    {
        foreach (var path in EnumerateItemPathCandidates(entity))
        {
            if (GroundItemPath.IsLootPath(path) || CurrencyPathMapper.IsGroundLootPath(path))
            {
                return path;
            }
        }

        return entity.Path;
    }

    public static void LogDetectionSample(AreaInstance area)
    {
        var worldItems = 0;
        var accepted = 0;
        var useless = 0;
        foreach (var (_, entity) in area.AwakeEntities)
        {
            if (!entity.IsValid)
            {
                continue;
            }

            if (IsWorldItemPlaceholder(entity))
            {
                worldItems++;
                if (entity.EntityState is EntityStates.Useless)
                {
                    useless++;
                }

                if (IsGroundLootEntity(entity))
                {
                    accepted++;
                    var animatedPath = entity.TryGetComponent<Animated>(out var animated)
                        ? animated.Path
                        : null;
                    Log.Info(
                        $"world item: active=true state={entity.EntityState} type={entity.EntityType} " +
                        $"hasStack={entity.TryGetComponent<Stack>(out _)} " +
                        $"hasMagic={entity.TryGetComponent<ObjectMagicProperties>(out _)} " +
                        $"animatedPath={animatedPath}",
                        "Auto Loot");
                }
            }
        }

        Log.Info(
            $"world item scan: placeholders={worldItems} active={accepted} useless={useless} awake={area.AwakeEntities.Count}",
            "Auto Loot");
    }

    public static bool IsWorldItemPlaceholder(Entity entity) =>
        IsWorldItemPath(entity.Path);

    public static bool IsWorldItemPath(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.Contains(WorldItemPlaceholderMarker, StringComparison.OrdinalIgnoreCase);

    private static bool HasDroppableItemComponents(Entity entity) =>
        entity.TryGetComponent<ObjectMagicProperties>(out _) ||
        entity.TryGetComponent<Mods>(out _) ||
        entity.TryGetComponent<Stack>(out _) ||
        entity.TryGetComponent<Animated>(out _);

    public static bool IsExcludedWorldObject(Entity entity)
    {
        if (entity.EntityType is EntityTypes.Monster or EntityTypes.Player or EntityTypes.NPC or
            EntityTypes.Chest or EntityTypes.Shrine)
        {
            return true;
        }

        if (entity.TryGetComponent<Transitionable>(out _))
        {
            return true;
        }

        if (entity.TryGetComponent<Shrine>(out _))
        {
            return true;
        }

        if (entity.TryGetComponent<NPC>(out _))
        {
            return true;
        }

        if (entity.TryGetComponent<Life>(out _))
        {
            return true;
        }

        var path = entity.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (IsWorldItemPlaceholder(entity))
        {
            return false;
        }

        if (path.Contains("Metadata/Effects/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (IsWorldObjectPath(path))
        {
            return true;
        }

        foreach (var marker in NonLootMiscellaneousMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        if (path.Contains("Metadata/MiscellaneousObjects/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Contains("Metadata/Terrain/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.Contains("Metadata/Monsters/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return path.Contains("Metadata/", StringComparison.OrdinalIgnoreCase) &&
               !path.Contains("Metadata/Items/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWorldObjectPath(string path)
    {
        if (IsCurrencyPortalScroll(path))
        {
            return false;
        }

        foreach (var marker in WorldObjectPathMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return path.Contains("Portal", StringComparison.OrdinalIgnoreCase) &&
               !path.Contains("/Currency/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCurrencyPortalScroll(string path) =>
        path.Contains("/Currency/", StringComparison.OrdinalIgnoreCase) &&
        path.Contains("Portal", StringComparison.OrdinalIgnoreCase);
}

internal static class GroundItemPath
{
    private static readonly string[] ItemLootMarkers =
    [
        "/Currency/",
        "/StackableCurrency/",
        "/Fragments/",
        "/Essences/",
        "/Essence/",
        "/Runes/",
        "/Rune/",
        "/Splinters/",
        "/Splinter/",
        "/Omen/",
        "/Omens/",
        "/Catalysts/",
        "/Scarab/",
        "/SoulCore/",
        "/SoulCores/",
        "/Tablet/",
        "/Tablets/",
        "/Waystone/",
        "/Waystones/",
        "/UncutGem/",
        "/UncutGems/",
        "/Incursion/",
        "/Breach/",
        "/Delirium/",
        "/Ritual/",
        "/Expedition/",
        "/Expedition2/",
        "/Verisium/",
        "/Abyss/",
        "/Rings/",
        "/Amulets/",
        "/Belts/",
        "/Armours/",
        "/Weapons/",
        "/Gems/",
        "/Jewels/",
        "/Maps/",
        "/Flasks/",
    ];

    public static bool IsLootPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (!path.Contains("Metadata/Items/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var marker in ItemLootMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return path.Contains("/Currency", StringComparison.OrdinalIgnoreCase);
    }
}
