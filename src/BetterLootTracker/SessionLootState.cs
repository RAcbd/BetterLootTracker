namespace BetterLootTracker;

public sealed class LootEntry
{
    public required string ItemPath { get; init; }
    public required string DisplayName { get; init; }
    public required string? PriceId { get; init; }
    public required int Quantity { get; init; }
    public required double DivineValue { get; init; }
    public required DateTime TimestampUtc { get; init; }
    public required string AreaName { get; init; }
}

public sealed class LootTotals
{
    public readonly Dictionary<string, int> QuantitiesByPath = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string> DisplayNamesByPath = new(StringComparer.Ordinal);
    public readonly Dictionary<string, string?> PriceIdsByPath = new(StringComparer.Ordinal);
    public double DivineEquivalent;
    public int PickupCount;
    public DateTime StartedUtc = DateTime.UtcNow;

    public bool HasLoot => QuantitiesByPath.Count > 0;

    public void Clear()
    {
        QuantitiesByPath.Clear();
        DisplayNamesByPath.Clear();
        PriceIdsByPath.Clear();
        DivineEquivalent = 0;
        PickupCount = 0;
        StartedUtc = DateTime.UtcNow;
    }

    public void CopyFrom(LootTotals other)
    {
        Clear();
        foreach (var (path, qty) in other.QuantitiesByPath)
        {
            QuantitiesByPath[path] = qty;
        }

        foreach (var (path, name) in other.DisplayNamesByPath)
        {
            DisplayNamesByPath[path] = name;
        }

        foreach (var (path, id) in other.PriceIdsByPath)
        {
            PriceIdsByPath[path] = id;
        }

        DivineEquivalent = other.DivineEquivalent;
        PickupCount = other.PickupCount;
        StartedUtc = other.StartedUtc;
    }
}

public sealed class MapLootSnapshot
{
    public string ZoneName = string.Empty;
    public string AreaId = string.Empty;
    public LootTotals Loot { get; } = new();
    public DateTime CompletedUtc;

    public bool HasZone => !string.IsNullOrWhiteSpace(ZoneName);

    public bool HasCurrency => Loot.HasLoot;

    public void SetFrom(string zoneName, string areaId, LootTotals source)
    {
        ZoneName = zoneName;
        AreaId = areaId;
        Loot.CopyFrom(source);
        CompletedUtc = DateTime.UtcNow;
    }

    public void Clear()
    {
        ZoneName = string.Empty;
        AreaId = string.Empty;
        Loot.Clear();
        CompletedUtc = default;
    }
}

public sealed class SessionLootState
{
    public LootTotals Session { get; } = new();
    public LootTotals CurrentMap { get; } = new();
    public MapLootSnapshot LastMap { get; } = new();
    public MapLootSnapshot BestMap { get; } = new();
    public List<LootEntry> RecentPickups { get; } = [];

    public string ActiveMapZoneName = string.Empty;
    public string ActiveMapAreaId = string.Empty;
    public bool ActiveMapIsTown;
    public bool TrackingPaused;

    public void ResetSession()
    {
        Session.Clear();
        CurrentMap.Clear();
        LastMap.Clear();
        BestMap.Clear();
        RecentPickups.Clear();
        ActiveMapZoneName = string.Empty;
        ActiveMapAreaId = string.Empty;
        ActiveMapIsTown = false;
    }

    public void ResetCurrentMap()
    {
        CurrentMap.Clear();
        CurrentMap.StartedUtc = DateTime.UtcNow;
    }

    public void BeginCurrentMap(string zoneName, string areaId, bool isTown)
    {
        ActiveMapZoneName = zoneName;
        ActiveMapAreaId = areaId;
        ActiveMapIsTown = isTown;
        ResetCurrentMap();
    }

    public void RecordPickup(
        string itemPath,
        string displayName,
        string? priceId,
        int quantity,
        double divineValue,
        string areaName,
        int maxRecentEntries)
    {
        if (quantity <= 0)
        {
            return;
        }

        AddToTotals(Session, itemPath, displayName, priceId, quantity, divineValue);
        AddToTotals(CurrentMap, itemPath, displayName, priceId, quantity, divineValue);

        RecentPickups.Insert(0, new LootEntry
        {
            ItemPath = itemPath,
            DisplayName = displayName,
            PriceId = priceId,
            Quantity = quantity,
            DivineValue = divineValue,
            TimestampUtc = DateTime.UtcNow,
            AreaName = areaName,
        });

        if (RecentPickups.Count > maxRecentEntries)
        {
            RecentPickups.RemoveRange(maxRecentEntries, RecentPickups.Count - maxRecentEntries);
        }
    }

    public void FinalizeCurrentMap()
    {
        if (!ActiveMapIsTown && !string.IsNullOrWhiteSpace(ActiveMapZoneName))
        {
            LastMap.SetFrom(ActiveMapZoneName, ActiveMapAreaId, CurrentMap);

            if (CurrentMap.HasLoot && IsBetterThanBest(LastMap, BestMap))
            {
                BestMap.SetFrom(LastMap.ZoneName, LastMap.AreaId, LastMap.Loot);
            }
        }

        ActiveMapZoneName = string.Empty;
        ActiveMapAreaId = string.Empty;
        ActiveMapIsTown = false;
        ResetCurrentMap();
    }

    private static bool IsBetterThanBest(MapLootSnapshot candidate, MapLootSnapshot currentBest)
    {
        if (!candidate.HasCurrency)
        {
            return false;
        }

        if (!currentBest.HasCurrency)
        {
            return true;
        }

        if (candidate.Loot.DivineEquivalent > currentBest.Loot.DivineEquivalent)
        {
            return true;
        }

        if (candidate.Loot.DivineEquivalent < currentBest.Loot.DivineEquivalent)
        {
            return false;
        }

        return candidate.Loot.PickupCount > currentBest.Loot.PickupCount;
    }

    private static void AddToTotals(
        LootTotals totals,
        string itemPath,
        string displayName,
        string? priceId,
        int quantity,
        double divineValue)
    {
        totals.QuantitiesByPath[itemPath] = totals.QuantitiesByPath.GetValueOrDefault(itemPath) + quantity;
        totals.DisplayNamesByPath[itemPath] = displayName;
        totals.PriceIdsByPath[itemPath] = priceId;
        totals.DivineEquivalent += divineValue;
        totals.PickupCount += quantity;
    }
}
