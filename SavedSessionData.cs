namespace BetterLootTracker;

public sealed class SavedLootTotalsData
{
    public Dictionary<string, int> QuantitiesByPath { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string> DisplayNamesByPath { get; set; } = new(StringComparer.Ordinal);

    public Dictionary<string, string?> PriceIdsByPath { get; set; } = new(StringComparer.Ordinal);

    public double DivineEquivalent { get; set; }

    public int PickupCount { get; set; }

    public static SavedLootTotalsData From(LootTotals totals) =>
        new()
        {
            QuantitiesByPath = new Dictionary<string, int>(totals.QuantitiesByPath, StringComparer.Ordinal),
            DisplayNamesByPath = new Dictionary<string, string>(totals.DisplayNamesByPath, StringComparer.Ordinal),
            PriceIdsByPath = new Dictionary<string, string?>(totals.PriceIdsByPath, StringComparer.Ordinal),
            DivineEquivalent = totals.DivineEquivalent,
            PickupCount = totals.PickupCount,
        };

    public void CopyTo(LootTotals totals)
    {
        totals.Clear();
        foreach (var (path, qty) in QuantitiesByPath)
        {
            totals.QuantitiesByPath[path] = qty;
        }

        foreach (var (path, name) in DisplayNamesByPath)
        {
            totals.DisplayNamesByPath[path] = name;
        }

        foreach (var (path, id) in PriceIdsByPath)
        {
            totals.PriceIdsByPath[path] = id;
        }

        totals.DivineEquivalent = DivineEquivalent;
        totals.PickupCount = PickupCount;
    }
}

public sealed class SavedMapData
{
    public string ZoneName { get; set; } = string.Empty;

    public string AreaId { get; set; } = string.Empty;

    public SavedLootTotalsData Loot { get; set; } = new();

    public DateTime CompletedUtc { get; set; }

    public static SavedMapData From(MapLootSnapshot snapshot) =>
        new()
        {
            ZoneName = snapshot.ZoneName,
            AreaId = snapshot.AreaId,
            Loot = SavedLootTotalsData.From(snapshot.Loot),
            CompletedUtc = snapshot.CompletedUtc,
        };

    public void CopyTo(MapLootSnapshot snapshot)
    {
        snapshot.ZoneName = ZoneName;
        snapshot.AreaId = AreaId;
        Loot.CopyTo(snapshot.Loot);
        snapshot.CompletedUtc = CompletedUtc;
    }
}

public sealed class SavedSessionFile
{
    public string Id { get; set; } = string.Empty;

    public DateTime SavedAtUtc { get; set; }

    public string? LastZoneName { get; set; }

    public SavedLootTotalsData Session { get; set; } = new();

    public SavedMapData? BestMap { get; set; }

    public SavedMapData? LastMap { get; set; }

    public static SavedSessionFile FromState(SessionLootState state, string id)
    {
        var file = new SavedSessionFile
        {
            Id = id,
            SavedAtUtc = DateTime.UtcNow,
            LastZoneName = string.IsNullOrWhiteSpace(state.ActiveMapZoneName)
                ? state.LastMap.ZoneName
                : state.ActiveMapZoneName,
            Session = SavedLootTotalsData.From(state.Session),
        };

        if (state.BestMap.HasCurrency)
        {
            file.BestMap = SavedMapData.From(state.BestMap);
        }

        if (state.LastMap.HasCurrency)
        {
            file.LastMap = SavedMapData.From(state.LastMap);
        }

        return file;
    }
}

public readonly record struct SavedSessionSummary(string Id, DateTime SavedAtUtc, string? LastZoneName, double DivineEquivalent);
