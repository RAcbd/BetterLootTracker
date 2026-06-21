namespace BetterLootTracker;

using System.Reflection;
using OriathHub.RemoteEnums;
using OriathHub.RemoteObjects;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;

internal static class HostInventoryBinder
{
    private static readonly MethodInfo? UpdateDataMethod = typeof(RemoteObjectBase).GetMethod(
        "UpdateData",
        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

    private static readonly int[] BindableInventoryIds =
    [
        (int)InventoryName.Currency1,
        (int)InventoryName.MapCurrency1,
        (int)InventoryName.EndgameSplinters1,
        (int)InventoryName.ExpandedInventory1,
    ];

    private static bool loggedBindTargets;
    private static bool loggedBindResult;

    public static void TryBindCurrencyInventories(
        ServerData serverData,
        IReadOnlyList<ServerDataInventoryReader.NativeInventoryEntry> nativeEntries,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems)
    {
        var placeholders = FindUnboundInventories(serverData).ToArray();
        if (placeholders.Length == 0)
        {
            return;
        }

        var bindTargets = nativeEntries
            .Where(static entry => BindableInventoryIds.Contains(entry.InventoryId))
            .ToArray();

        if (!loggedBindTargets)
        {
            loggedBindTargets = true;
            var targetSummary = string.Join(
                ", ",
                bindTargets.Select(static entry =>
                    $"{ServerDataInventoryReader.LabelForInventory(entry.InventoryId)}@{entry.InventoryPtr.ToInt64():X}"));
            Log.Info(
                $"bind targets: {targetSummary}; placeholders: {string.Join(", ", placeholders.Select(static p => p.Name))}",
                "Better Loot Tracker");
        }

        var bindCount = Math.Min(placeholders.Length, bindTargets.Length);
        for (var i = 0; i < bindCount; i++)
        {
            var (name, inventory) = placeholders[i];
            var target = bindTargets[i];
            if (target.InventoryPtr == IntPtr.Zero)
            {
                continue;
            }

            inventory.Address = target.InventoryPtr;
            ForceUpdate(inventory);

            if (!loggedBindResult)
            {
                loggedBindResult = true;
                Log.Info(
                    $"after bind '{name}': addr={inventory.Address.ToInt64():X}, " +
                    $"items={inventory.Items.Count}, grid={inventory.TotalBoxes.X}x{inventory.TotalBoxes.Y}",
                    "Better Loot Tracker");
            }

            var before = entities.Count;
            InventoryScanner.ScanHostInventory(name, inventory, entities, seenItems);

            if (entities.Count > before)
            {
                Log.Info(
                    $"bound '{name}' -> {ServerDataInventoryReader.LabelForInventory(target.InventoryId)} " +
                    $"({entities.Count} entity stacks)",
                    "Better Loot Tracker");
            }
        }
    }

    public static void ResetDiagnostics()
    {
        loggedBindTargets = false;
        loggedBindResult = false;
    }

    private static IEnumerable<(string Name, Inventory Inventory)> FindUnboundInventories(ServerData serverData)
    {
        foreach (var (name, inventory) in InventoryScanner.EnumerateHostInventories(serverData))
        {
            if (inventory.Address == IntPtr.Zero && name is "a" or "A")
            {
                yield return (name, inventory);
            }
        }
    }

    private static void ForceUpdate(RemoteObjectBase remoteObject) =>
        UpdateDataMethod?.Invoke(remoteObject, [true]);
}
