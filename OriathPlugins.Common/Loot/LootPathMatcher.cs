namespace OriathPlugins.Common.Loot;

using OriathHub.RemoteEnums;
using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Pricing;

public static class LootPathMatcher
{
    private static readonly string[] StackableMarkers =
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
    ];

    private static readonly string[] EquipmentMarkers =
    [
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

    private static readonly string[] AlwaysPickupMarkers =
    [
        "/Waystone/",
        "/Waystones/",
        "/Tablet/",
        "/Tablets/",
    ];

    public static bool IsGoldPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        if (path.Contains("/GoldCoin", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/Currency/Gold", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("CoinPile", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var fileName = GetFileName(path);
        return fileName.Equals("Gold", StringComparison.OrdinalIgnoreCase) ||
               fileName.Equals("GoldCoin", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsGoldEntity(Entity entity)
    {
        foreach (var path in GroundLootRules.EnumerateItemPathCandidates(entity))
        {
            if (IsGoldPath(path))
            {
                return true;
            }
        }

        var resolved = GroundLootRules.ResolveItemPath(entity);
        if (IsGoldPath(resolved))
        {
            return true;
        }

        var displayName = GroundLootRules.ResolveDisplayName(entity);
        return displayName.Equals("Gold", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldPickup(
        Entity entity,
        string itemPath,
        double divineValue,
        GroundLootScanSettings settings,
        LootPriceService? prices)
    {
        if (!GroundLootRules.IsGroundLootEntity(entity))
        {
            return false;
        }

        if (IsGoldEntity(entity))
        {
            return false;
        }

        if (!settings.CurrencyOnly)
        {
            return PassesValueFilter(divineValue, settings, prices, itemPath);
        }

        if (!IsCurrencyPickup(entity, itemPath, settings.AlwaysPickupWaystonesAndTablets))
        {
            return false;
        }

        return PassesValueFilter(divineValue, settings, prices, itemPath);
    }

    public static bool IsCurrencyPickup(Entity entity, string? itemPath, bool alwaysPickupWaystonesAndTablets)
    {
        foreach (var path in GroundLootRules.EnumerateItemPathCandidates(entity))
        {
            if (IsGoldPath(path))
            {
                return false;
            }

            if (alwaysPickupWaystonesAndTablets && IsAlwaysPickupPath(path))
            {
                return true;
            }

            if (IsCurrencyLootPath(path))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(itemPath))
        {
            if (IsGoldPath(itemPath))
            {
                return false;
            }

            if (alwaysPickupWaystonesAndTablets && IsAlwaysPickupPath(itemPath))
            {
                return true;
            }

            if (IsCurrencyLootPath(itemPath))
            {
                return true;
            }
        }

        if (IsEquipmentDrop(entity) || IsGoldEntity(entity))
        {
            return false;
        }

        // Currency orbs often appear as WorldItem placeholders before Animated paths resolve.
        if (GroundLootRules.IsWorldItemPlaceholder(entity) ||
            entity.EntitySubtype is EntitySubtypes.WorldItem)
        {
            return true;
        }

        return false;
    }

    public static bool IsAlwaysPickupPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var marker in AlwaysPickupMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static bool MatchesStackablePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var marker in StackableMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return path.Contains("/Currency", StringComparison.OrdinalIgnoreCase);
    }

    public static string? GetNinjaIdHint(string itemPath)
    {
        var fileName = GetFileName(itemPath);
        if (fileName.Contains("ModValues", StringComparison.OrdinalIgnoreCase))
        {
            return "divine";
        }

        return null;
    }

    public static string GetDisplayName(Entity entity) =>
        GroundLootRules.ResolveDisplayName(entity);

    public static string GetDisplayName(string itemPath)
    {
        var fileName = itemPath.Replace('\\', '/').Split('/').LastOrDefault() ?? itemPath;
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "Unknown";
        }

        var chars = new List<char>(fileName.Length + 8);
        for (var i = 0; i < fileName.Length; i++)
        {
            var c = fileName[i];
            if (c is '_' or '-')
            {
                chars.Add(' ');
                continue;
            }

            if (i > 0 && char.IsUpper(c) && !char.IsUpper(fileName[i - 1]))
            {
                chars.Add(' ');
            }

            chars.Add(i == 0 ? char.ToUpperInvariant(c) : c);
        }

        return new string(chars.ToArray());
    }

    private static bool IsCurrencyLootPath(string path) =>
        !IsGoldPath(path) &&
        (CurrencyPathMapper.IsTrackableCurrencyPath(path) || IsLooseCurrencyPath(path));

    private static bool IsLooseCurrencyPath(string path) =>
        path.Contains("/Currency/", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/StackableCurrency/", StringComparison.OrdinalIgnoreCase);

    private static bool IsEquipmentDrop(Entity entity)
    {
        if (entity.TryGetComponent<ObjectMagicProperties>(out var magic) &&
            magic.Rarity is Rarity.Rare or Rarity.Unique)
        {
            return true;
        }

        if (entity.TryGetComponent<Mods>(out _))
        {
            return true;
        }

        foreach (var path in GroundLootRules.EnumerateItemPathCandidates(entity))
        {
            if (IsEquipmentPath(path))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsEquipmentPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (var marker in EquipmentMarkers)
        {
            if (path.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PassesValueFilter(
        double divineValue,
        GroundLootScanSettings settings,
        LootPriceService? prices,
        string itemPath)
    {
        if (!settings.UseValueFilter)
        {
            return true;
        }

        if (settings.AlwaysPickupWaystonesAndTablets && IsAlwaysPickupPath(itemPath))
        {
            return true;
        }

        if (divineValue >= settings.MinDivineValue)
        {
            return true;
        }

        if (divineValue <= 0d && prices?.HasPriceData != true &&
            (MatchesStackablePath(itemPath) || CurrencyPathMapper.IsTrackableCurrencyPath(itemPath)))
        {
            return true;
        }

        return false;
    }

    private static string GetFileName(string itemPath) =>
        itemPath.Replace('\\', '/').Split('/').LastOrDefault() ?? itemPath;
}
