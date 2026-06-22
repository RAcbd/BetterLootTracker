namespace BetterLootTracker;

using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;
using OriathPlugins.Common.Inventory;
using OriathPlugins.Common.Pricing;

internal sealed class LootTrackerService
{
    private readonly WorldPickupTracker worldPickupTracker = new();
    private readonly Dictionary<string, int> lastQuantitiesByPath = new(StringComparer.Ordinal);
    private readonly CurrencyDisplayNameStore displayNames = new();
    private readonly SessionHistoryStore sessionHistory = new();
    private NinjaPriceCatalog priceCatalog = NinjaPriceCatalog.Empty;
    private string previousAreaId = string.Empty;
    private bool inventoryBaselineReady;
    private int baselineWaitFrames;
    private const int MaxBaselineWaitFrames = 120;

    public SessionHistoryStore SessionHistory => sessionHistory;

    public string CurrencyNamesFilePath => displayNames.DataFilePath;

    public string? PriceLoadError { get; private set; }

    public bool HasPriceData => priceCatalog.IsLoaded;

    public string PriceStatusMessage { get; private set; } = "Prices not loaded.";

    public bool TryGetDivineUnitValue(string? priceId, out double value) =>
        priceCatalog.TryGetDivineValue(priceId, out value);

    public string? ResolvePriceId(string itemPath, LootTotals totals)
    {
        var storedId = totals.PriceIdsByPath.GetValueOrDefault(itemPath);
        if (!string.IsNullOrWhiteSpace(storedId) && priceCatalog.TryNormalizeId(storedId, out var normalizedStored))
        {
            return normalizedStored;
        }

        var fallbackName = totals.DisplayNamesByPath.GetValueOrDefault(itemPath);
        return PriceResolver.Resolve(itemPath, fallbackName, priceCatalog, displayNames);
    }

    public bool TryGetDivineUnitValueForItem(string itemPath, LootTotals totals, out double value)
    {
        var priceId = ResolvePriceId(itemPath, totals);
        return TryGetDivineUnitValue(priceId, out value);
    }

    public string? ResolvePriceId(string itemPath, string? displayName) =>
        PriceResolver.Resolve(itemPath, displayName, priceCatalog, displayNames);

    public IReadOnlyList<CurrencyOption> GetCurrencyOptions() =>
        priceCatalog.GetCurrencyOptions();

    public void InitializeData(string dllDirectory)
    {
        displayNames.Load(dllDirectory);
        sessionHistory.Initialize(dllDirectory);
    }

    public void ReloadCurrencyNames(string dllDirectory) => displayNames.Load(dllDirectory);

    public string ResolveDisplayName(string itemPath, string? priceId, string? fallbackName) =>
        PriceResolver.ResolveDisplayName(itemPath, priceId, fallbackName, displayNames, priceCatalog);

    public string GetItemDisplayName(string itemPath, LootTotals totals)
    {
        var priceId = ResolvePriceId(itemPath, totals);
        return ResolveDisplayName(
            itemPath,
            priceId,
            totals.DisplayNamesByPath.GetValueOrDefault(itemPath, itemPath));
    }

    public void RegisterCurrencyDiscovery(string itemPath, string? priceId, string displayName) =>
        displayNames.TryRegisterDiscovery(itemPath, priceId, displayName, priceCatalog);

    public void RefreshStoredPrices(SessionLootState state)
    {
        RefreshTotalsPrices(state.Session);
        RefreshTotalsPrices(state.CurrentMap);
        RefreshTotalsPrices(state.LastMap.Loot);
        RefreshTotalsPrices(state.BestMap.Loot);
    }

    private void RefreshTotalsPrices(LootTotals totals)
    {
        if (!totals.HasLoot)
        {
            return;
        }

        totals.DivineEquivalent = 0;
        foreach (var itemPath in totals.QuantitiesByPath.Keys.ToArray())
        {
            var quantity = totals.QuantitiesByPath[itemPath];
            var priceId = ResolvePriceId(itemPath, totals);
            totals.DisplayNamesByPath[itemPath] = GetItemDisplayName(itemPath, totals);
            totals.PriceIdsByPath[itemPath] = priceId;

            if (TryGetDivineUnitValue(priceId, out var unitValue))
            {
                totals.DivineEquivalent += unitValue * quantity;
            }
        }
    }

    public bool TrySaveSession(SessionLootState state) => sessionHistory.TrySaveSession(state);

    public MapLootSnapshot GetLastSessionView(bool useLastSession)
    {
        if (!useLastSession)
        {
            return new MapLootSnapshot();
        }

        return sessionHistory.CreateLastSessionView();
    }

    public void ReloadPrices(string? leagueDataDirectory)
    {
        if (string.IsNullOrWhiteSpace(leagueDataDirectory))
        {
            priceCatalog = NinjaPriceCatalog.Empty;
            PriceLoadError = "Ninja pricing disabled.";
            PriceStatusMessage = PriceLoadError;
            return;
        }

        if (NinjaPriceLoader.TryLoadLeagueCatalog(leagueDataDirectory, out var catalog, out var status))
        {
            priceCatalog = catalog;
            PriceLoadError = null;
            PriceStatusMessage = status;
            BaseItemTypeResolver.Invalidate();
            return;
        }

        priceCatalog = NinjaPriceCatalog.Empty;
        PriceLoadError = status;
        PriceStatusMessage = status;
    }

    public void ResetSession(SessionLootState state)
    {
        previousAreaId = string.Empty;
        lastQuantitiesByPath.Clear();
        inventoryBaselineReady = false;
        baselineWaitFrames = 0;
        InventoryScanner.ResetDiagnostics();
        worldPickupTracker.Reset();
        state.ResetSession();
    }

    public void ProcessFrame(
        AreaInstance area,
        SessionLootState state,
        BetterLootTrackerSettings settings,
        string zoneName,
        string areaId,
        bool isTown,
        bool trackingAllowed)
    {
        if (!string.IsNullOrEmpty(previousAreaId) &&
            !previousAreaId.Equals(areaId, StringComparison.Ordinal))
        {
            state.FinalizeCurrentMap();
            worldPickupTracker.Reset();
            lastQuantitiesByPath.Clear();
            inventoryBaselineReady = false;
            baselineWaitFrames = 0;
            InventoryScanner.ResetDiagnostics();
        }

        if (string.IsNullOrEmpty(previousAreaId) || !previousAreaId.Equals(areaId, StringComparison.Ordinal))
        {
            state.BeginCurrentMap(zoneName, areaId, isTown);
            previousAreaId = areaId;
        }
        else
        {
            state.ActiveMapZoneName = zoneName;
            state.ActiveMapAreaId = areaId;
            state.ActiveMapIsTown = isTown;
        }

        if (!trackingAllowed)
        {
            state.TrackingPaused = true;
            return;
        }

        state.TrackingPaused = false;

        var pendingPickups = new Dictionary<string, CurrencyPickup>(StringComparer.Ordinal);

        foreach (var pickup in ProcessInventoryPickups(area.ServerDataObject, settings, zoneName))
        {
            StagePickup(pendingPickups, pickup);
        }

        foreach (var pickup in worldPickupTracker.ProcessFrame(area, priceCatalog, settings))
        {
            StagePickup(pendingPickups, pickup);
        }

        foreach (var pickup in pendingPickups.Values)
        {
            RecordPickupOnce(state, settings, zoneName, pickup);
        }
    }

    private static void StagePickup(Dictionary<string, CurrencyPickup> pending, CurrencyPickup pickup)
    {
        if (pending.TryGetValue(pickup.ItemPath, out var existing))
        {
            pending[pickup.ItemPath] = existing with { Quantity = existing.Quantity + pickup.Quantity };
            return;
        }

        pending[pickup.ItemPath] = pickup;
    }

    private IEnumerable<CurrencyPickup> ProcessInventoryPickups(
        ServerData serverData,
        BetterLootTrackerSettings settings,
        string zoneName)
    {
        var current = InventoryScanner.BuildEntitySnapshot(serverData, settings.EnableDebugLogging);
        var currentByPath = InventoryScanner.AggregateByPath(current);

        if (!inventoryBaselineReady)
        {
            baselineWaitFrames++;
            var hasCurrency = currentByPath.Count > 0;
            if (hasCurrency || baselineWaitFrames >= MaxBaselineWaitFrames)
            {
                ApplyInventoryBaseline(currentByPath, settings.EnableDebugLogging, hasCurrency);
            }

            yield break;
        }

        foreach (var (itemPath, count) in currentByPath)
        {
            var gained = count - lastQuantitiesByPath.GetValueOrDefault(itemPath);
            if (gained <= 0)
            {
                continue;
            }

            var priceId = CurrencyPathMapper.TryMapToNinjaId(itemPath, priceCatalog);
            var displayName = ResolveDisplayName(
                itemPath,
                priceId,
                CurrencyPathMapper.GetDisplayName(itemPath, priceCatalog.TryGetDisplayName(priceId)));
            priceId = ResolvePriceId(itemPath, displayName);
            if (!CurrencyFilter.ShouldTrack(settings, priceId, itemPath))
            {
                continue;
            }

            if (settings.EnableDebugLogging)
            {
                Log.Info(
                    $"inventory +{gained}x {displayName} ({itemPath}) in {zoneName}",
                    "Better Loot Tracker");
            }

            yield return new CurrencyPickup(itemPath, priceId, displayName, gained);
        }

        lastQuantitiesByPath.Clear();
        foreach (var (path, count) in currentByPath)
        {
            lastQuantitiesByPath[path] = count;
        }
    }

    private void ApplyInventoryBaseline(
        Dictionary<string, int> snapshotByPath,
        bool enableDebugLogging,
        bool hasCurrency)
    {
        lastQuantitiesByPath.Clear();
        foreach (var (path, count) in snapshotByPath)
        {
            lastQuantitiesByPath[path] = count;
        }

        inventoryBaselineReady = true;

        Log.Info(
            $"inventory baseline: {snapshotByPath.Count} currency types, {snapshotByPath.Values.Sum()} total stacks",
            "Better Loot Tracker");

        if (enableDebugLogging && !hasCurrency)
        {
            Log.Info(
                "inventory baseline is empty — only new pickups will be counted",
                "Better Loot Tracker");
        }
    }

    private void RecordPickupOnce(
        SessionLootState state,
        BetterLootTrackerSettings settings,
        string zoneName,
        CurrencyPickup pickup)
    {
        var priceId = ResolvePriceId(pickup.ItemPath, pickup.DisplayName) ?? pickup.PriceId;
        var displayName = ResolveDisplayName(pickup.ItemPath, priceId, pickup.DisplayName);
        var catalogName = priceCatalog.TryGetDisplayName(priceId);
        if (!string.IsNullOrWhiteSpace(catalogName))
        {
            displayName = catalogName;
        }

        var divinePerUnit = 0d;
        if (settings.ShowDivineEquivalent && TryGetDivineUnitValue(priceId, out var unitValue))
        {
            divinePerUnit = unitValue;
        }

        if (settings.EnableDebugLogging)
        {
            Log.Info($"recorded: {pickup.Quantity}x {displayName}", "Better Loot Tracker");
        }

        RegisterCurrencyDiscovery(pickup.ItemPath, priceId, displayName);

        state.RecordPickup(
            pickup.ItemPath,
            displayName,
            priceId,
            pickup.Quantity,
            divinePerUnit * pickup.Quantity,
            zoneName,
            settings.MaxRecentEntries);
    }

    public double GetDisplayedValue(double divineValue, string unit) =>
        unit.ToLowerInvariant() switch
        {
            "chaos" => divineValue * GetChaosPerDivine(),
            "exalted" => divineValue * GetExaltedPerDivine(),
            _ => divineValue,
        };

    public string GetValueSuffix(string unit) =>
        unit.ToLowerInvariant() switch
        {
            "chaos" => "c",
            "exalted" => "ex",
            _ => "d",
        };

    private double GetChaosPerDivine()
    {
        if (priceCatalog.TryGetDivineValue("chaos", out var chaosDivineValue) && chaosDivineValue > 0)
        {
            return 1d / chaosDivineValue;
        }

        return 10d;
    }

    private double GetExaltedPerDivine()
    {
        if (priceCatalog.TryGetDivineValue("exalted", out var exaltedDivineValue) && exaltedDivineValue > 0)
        {
            return 1d / exaltedDivineValue;
        }

        return 250d;
    }
}
