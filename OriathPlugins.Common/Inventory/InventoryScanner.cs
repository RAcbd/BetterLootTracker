namespace OriathPlugins.Common.Inventory;

using OriathHub.RemoteEnums;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;
using OriathPlugins.Common.Pricing;

public readonly record struct InventoryEntityStack(string ItemPath, int StackCount);

public static class InventoryScanner
{
    public static readonly InventoryName[] TrackedInventories =
    [
        InventoryName.MainInventory1,
        InventoryName.Currency1,
        InventoryName.MapCurrency1,
        InventoryName.EndgameSplinters1,
        InventoryName.ExpandedInventory1,
    ];

    private static readonly HashSet<InventoryName> TrackedInventorySet = new(TrackedInventories);

    private static bool loggedInventoryProbe;

    public static Dictionary<string, int> BuildCurrencySnapshot(ServerData serverData, bool debugLogging) =>
        AggregateByPath(BuildEntitySnapshot(serverData, debugLogging));

    public static bool HaveTrackedInventoriesChanged(
        ServerData serverData,
        Dictionary<InventoryName, int> lastRequestCounters)
    {
        var changed = false;
        foreach (var inventoryName in TrackedInventories)
        {
            if (!serverData.AvailableInventories.Contains(inventoryName))
            {
                continue;
            }

            var inventory = serverData.GetInventory(inventoryName);
            if (!IsReadableInventory(inventory))
            {
                continue;
            }

            var counter = inventory.ServerRequestCounter;
            if (lastRequestCounters.TryGetValue(inventoryName, out var previous) && previous == counter)
            {
                continue;
            }

            lastRequestCounters[inventoryName] = counter;
            changed = true;
        }

        return changed;
    }

    public static Dictionary<nint, InventoryEntityStack> BuildEntitySnapshot(ServerData serverData, bool debugLogging)
    {
        var entities = new Dictionary<nint, InventoryEntityStack>();
        var seenItems = new HashSet<nint>();

        foreach (var inventoryName in serverData.AvailableInventories)
        {
            if (!TrackedInventorySet.Contains(inventoryName))
            {
                continue;
            }

            var inventory = serverData.GetInventory(inventoryName);
            if (!IsReadableInventory(inventory))
            {
                continue;
            }

            var label = inventoryName.ToString();
            var before = entities.Count;
            ScanInventory(inventoryName, label, inventory, entities, seenItems);

            if (debugLogging && entities.Count > before)
            {
                Log.Info($"inventory '{label}' contributed loot items", "Better Loot Tracker");
            }
        }

        if (!loggedInventoryProbe)
        {
            loggedInventoryProbe = true;
            var summary = serverData.AvailableInventories
                .Select(name =>
                {
                    var inv = serverData.GetInventory(name);
                    return $"{name}@{inv.Address.ToInt64():X}:items={inv.Items.Count}";
                })
                .OrderBy(static line => line, StringComparer.Ordinal);
            Log.Info($"available inventories: {string.Join(", ", summary)}", "Better Loot Tracker");
        }

        return entities;
    }

    public static Dictionary<string, int> AggregateByPath(IReadOnlyDictionary<nint, InventoryEntityStack> entities)
    {
        var totals = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var (_, stack) in entities)
        {
            totals[stack.ItemPath] = totals.GetValueOrDefault(stack.ItemPath) + stack.StackCount;
        }

        return totals;
    }

    public static void ResetDiagnostics() => loggedInventoryProbe = false;

    private static bool IsReadableInventory(Inventory inventory) =>
        inventory.TotalBoxes.X > 0 && inventory.TotalBoxes.Y > 0;

    private static void ScanInventory(
        InventoryName inventoryName,
        string inventoryLabel,
        Inventory inventory,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems)
    {
        var itemCount = 0;
        foreach (var entry in inventory.Entries)
        {
            if (TryAddItem(inventoryName, entry.Item, entities, seenItems))
            {
                itemCount++;
            }
        }

        if (itemCount == 0)
        {
            var rows = inventory.TotalBoxes.Y;
            var columns = inventory.TotalBoxes.X;
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    if (TryAddItem(inventoryName, inventory[y, x], entities, seenItems))
                    {
                        itemCount++;
                    }
                }
            }

            foreach (var item in inventory.Items.Values)
            {
                if (TryAddItem(inventoryName, item, entities, seenItems))
                {
                    itemCount++;
                }
            }
        }

        if (inventoryLabel is "Currency1" && itemCount > 0)
        {
            Log.Info(
                $"inventory '{inventoryLabel}': {itemCount} readable items, {entities.Count} entity stacks",
                "Better Loot Tracker");
        }
    }

    private static bool TryAddItem(
        InventoryName inventoryName,
        Item item,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems)
    {
        if (!item.IsValid || !seenItems.Add(item.Address))
        {
            return false;
        }

        var path = item.Path;
        if (!ShouldTrackItemPath(inventoryName, path))
        {
            return false;
        }

        entities[item.Address] = new InventoryEntityStack(path, ItemStackCountReader.Read(item));
        return true;
    }

    private static bool ShouldTrackItemPath(InventoryName inventoryName, string path) =>
        CurrencyPathMapper.IsTrackableCurrencyPath(path) ||
        (inventoryName == InventoryName.MainInventory1 && CurrencyPathMapper.IsInventoryLootPath(path));
}
