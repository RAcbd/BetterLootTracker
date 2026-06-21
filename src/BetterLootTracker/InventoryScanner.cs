namespace BetterLootTracker;

using System.Reflection;
using OriathHub.RemoteEnums;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;

internal readonly record struct InventoryEntityStack(string ItemPath, int StackCount);

internal static class InventoryScanner
{
    private static readonly string[] PreferredInventoryNames =
    [
        "Currency1",
        "MapCurrency1",
        "EndgameSplinters1",
        "ExpandedInventory1",
        "MainInventory1",
    ];

    private static MemberInfo[]? cachedInventoryMembers;
    private static bool loggedHostProbe;

    public static Dictionary<string, int> BuildCurrencySnapshot(ServerData serverData, bool debugLogging) =>
        AggregateByPath(BuildEntitySnapshot(serverData, debugLogging));

    public static Dictionary<nint, InventoryEntityStack> BuildEntitySnapshot(ServerData serverData, bool debugLogging)
    {
        var entities = new Dictionary<nint, InventoryEntityStack>();
        var seenItems = new HashSet<nint>();
        var nativeEntries = ServerDataInventoryReader.GetInventoryEntries(serverData);

        HostInventoryBinder.TryBindCurrencyInventories(serverData, nativeEntries, entities, seenItems);

        foreach (var (memberName, inventory) in EnumerateHostInventories(serverData))
        {
            var nativeId = nativeEntries
                .FirstOrDefault(entry => entry.InventoryPtr == inventory.Address)
                .InventoryId;

            if (!ShouldScanHostInventory(memberName, nativeId, inventory.Address))
            {
                continue;
            }

            var before = entities.Count;
            ScanHostInventory(memberName, inventory, entities, seenItems);

            if (debugLogging && entities.Count > before)
            {
                Log.Info(
                    $"host inventory '{memberName}' contributed currency items",
                    "Better Loot Tracker");
            }
        }

        ServerDataInventoryReader.ScanTrackedNativeInventories(serverData, entities, seenItems, debugLogging);

        if (!loggedHostProbe)
        {
            loggedHostProbe = true;
            var hostSummary = EnumerateHostInventories(serverData)
                .Select(pair => $"{pair.Name}@{pair.Inventory.Address.ToInt64():X}:items={pair.Inventory.Items.Count}")
                .OrderBy(static line => line, StringComparer.Ordinal);
            Log.Info($"host inventories: {string.Join(", ", hostSummary)}", "Better Loot Tracker");
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

    public static void ResetDiagnostics()
    {
        loggedHostProbe = false;
        HostInventoryBinder.ResetDiagnostics();
        ServerDataInventoryReader.ResetDiagnostics();
    }

    internal static bool ShouldScanHostInventory(string memberName, int nativeId, nint address)
    {
        if (address != IntPtr.Zero && memberName is "a" or "A")
        {
            return true;
        }

        if (PreferredInventoryNames.Contains(memberName, StringComparer.Ordinal))
        {
            return true;
        }

        return nativeId is (int)InventoryName.Currency1
            or (int)InventoryName.MapCurrency1
            or (int)InventoryName.EndgameSplinters1
            or (int)InventoryName.ExpandedInventory1;
    }

    internal static void ScanHostInventory(
        string memberName,
        Inventory inventory,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems)
    {
        var itemCount = 0;
        var rows = inventory.TotalBoxes.Y;
        var columns = inventory.TotalBoxes.X;

        if (rows > 0 && columns > 0)
        {
            for (var y = 0; y < rows; y++)
            {
                for (var x = 0; x < columns; x++)
                {
                    if (TryAddHostItem(inventory[y, x], entities, seenItems))
                    {
                        itemCount++;
                    }
                }
            }
        }

        foreach (var item in inventory.Items.Values)
        {
            if (TryAddHostItem(item, entities, seenItems))
            {
                itemCount++;
            }
        }

        if (memberName is "Currency1" or "a" or "A" && itemCount > 0)
        {
            Log.Info(
                $"host Currency tab '{memberName}': {itemCount} readable items, {entities.Count} entity stacks",
                "Better Loot Tracker");
        }
    }

    private static bool TryAddHostItem(
        Item item,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems)
    {
        if (!item.IsValid || !seenItems.Add(item.Address))
        {
            return false;
        }

        var path = item.Path;
        if (string.IsNullOrWhiteSpace(path))
        {
            path = ServerDataInventoryReader.TryReadItemPath(item.Address);
        }

        if (!CurrencyPathMapper.IsTrackableCurrencyPath(path))
        {
            return false;
        }

        entities[item.Address] = new InventoryEntityStack(path, ItemStackCountReader.Read(item));
        return true;
    }

    internal static IEnumerable<(string Name, Inventory Inventory)> EnumerateHostInventories(ServerData serverData)
    {
        var seen = new HashSet<Inventory>();
        var serverDataType = serverData.GetType();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        cachedInventoryMembers ??= serverDataType
            .GetProperties(flags)
            .Where(static member => member is PropertyInfo { PropertyType: var type } && type == typeof(Inventory))
            .Cast<MemberInfo>()
            .Concat(serverDataType
                .GetFields(flags)
                .Where(static member => member is FieldInfo { FieldType: var type } && type == typeof(Inventory)))
            .ToArray();

        foreach (var preferredName in PreferredInventoryNames)
        {
            var inventory = TryReadInventory(serverData, serverDataType, preferredName, flags);
            if (inventory is not null && seen.Add(inventory))
            {
                yield return (preferredName, inventory);
            }
        }

        foreach (var member in cachedInventoryMembers)
        {
            var inventory = ReadInventory(serverData, member);
            if (inventory is not null && seen.Add(inventory))
            {
                yield return (member.Name, inventory);
            }
        }
    }

    private static Inventory? TryReadInventory(
        ServerData serverData,
        Type serverDataType,
        string memberName,
        BindingFlags flags)
    {
        var property = serverDataType.GetProperty(memberName, flags);
        if (property?.GetValue(serverData) is Inventory propertyInventory)
        {
            return propertyInventory;
        }

        var field = serverDataType.GetField(memberName, flags);
        return field?.GetValue(serverData) as Inventory;
    }

    private static Inventory? ReadInventory(ServerData serverData, MemberInfo member) =>
        member switch
        {
            PropertyInfo property when property.PropertyType == typeof(Inventory) =>
                property.GetValue(serverData) as Inventory,
            FieldInfo field when field.FieldType == typeof(Inventory) =>
                field.GetValue(serverData) as Inventory,
            _ => null,
        };
}
