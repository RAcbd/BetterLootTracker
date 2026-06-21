namespace BetterLootTracker;

using System.Runtime.InteropServices;
using GameOffsets.Natives;
using OriathHub;
using OriathHub.RemoteEnums;
using OriathHub.RemoteObjects;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathHub.Utils;

internal static class ServerDataInventoryReader
{
    private const int ServerDataPlayerVectorOffset = 0x48;
    private const int PlayerInventoriesOffset = 0x320;
    private const int InventoryItemListOffset = 0x170;
    private const int InventoryItemSlotEntityOffset = 0x0;
    private const int ItemEntityDetailsPtrOffset = 0x8;
    private const int EntityDetailsNameOffset = 0x8;

    public static int ReadStackCount(nint itemEntity) => NativeItemComponentReader.ReadStackCount(itemEntity);

    private static readonly HashSet<int> loggedInventoryScanDebug = [];

    private static readonly int[] TrackedInventoryIds =
    [
        (int)InventoryName.MainInventory1,
        (int)InventoryName.Currency1,
        (int)InventoryName.MapCurrency1,
        (int)InventoryName.EndgameSplinters1,
        (int)InventoryName.ExpandedInventory1,
    ];

    private static bool loggedInventoryProbe;
    private static bool loggedCurrencyProbe;
    private static bool loggedTrackedScan;

    public readonly record struct NativeInventoryEntry(int InventoryId, nint InventoryPtr);

    public static IReadOnlyList<NativeInventoryEntry> GetInventoryEntries(ServerData serverData)
    {
        if (serverData.Address == IntPtr.Zero)
        {
            return [];
        }

        if (!Core.Process.ReadMemory(
                IntPtr.Add(serverData.Address, ServerDataPlayerVectorOffset),
                out StdVector playerVector))
        {
            return [];
        }

        var players = Core.Process.ReadStdVector<nint>(playerVector);
        if (players.Length == 0 || players[0] == IntPtr.Zero)
        {
            return [];
        }

        if (!Core.Process.ReadMemory(
                IntPtr.Add(players[0], PlayerInventoriesOffset),
                out StdVector inventoriesVector))
        {
            return [];
        }

        var inventories = Core.Process.ReadStdVector<InventoryEntryNative>(inventoriesVector);
        if (!loggedInventoryProbe)
        {
            loggedInventoryProbe = true;
            var names = inventories
                .Where(static entry => entry.InventoryPtr0 != IntPtr.Zero)
                .Select(static entry => $"{LabelFor(entry.InventoryId)}({entry.InventoryId})")
                .OrderBy(static name => name, StringComparer.Ordinal);
            Log.Info($"memory inventories: {string.Join(", ", names)}", "Better Loot Tracker");
        }

        return inventories
            .Where(static entry => entry.InventoryPtr0 != IntPtr.Zero)
            .Select(static entry => new NativeInventoryEntry(entry.InventoryId, entry.InventoryPtr0))
            .ToArray();
    }

    public static void ScanTrackedNativeInventories(
        ServerData serverData,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems,
        bool debugLogging)
    {
        var hits = new List<string>();
        foreach (var entry in GetInventoryEntries(serverData))
        {
            if (!TrackedInventoryIds.Contains(entry.InventoryId))
            {
                continue;
            }

            var before = entities.Count;
            ScanRawInventory(entry, entities, seenItems, debugLogging);

            if (entities.Count > before)
            {
                hits.Add($"{LabelForInventory(entry.InventoryId)}(+{entities.Count - before})");
            }
        }

        if (hits.Count == 0)
        {
            return;
        }

        if (debugLogging && !loggedTrackedScan)
        {
            loggedTrackedScan = true;
            Log.Info(
                $"tracked native inventories: {string.Join(", ", hits)}",
                "Better Loot Tracker");
        }
    }

    public static string LabelForInventory(int inventoryId) => LabelFor(inventoryId);

    public static string TryReadItemPath(nint itemEntityAddress)
    {
        if (itemEntityAddress == IntPtr.Zero)
        {
            return string.Empty;
        }

        return ReadItemPath(itemEntityAddress);
    }

    public static void ResetDiagnostics()
    {
        loggedInventoryProbe = false;
        loggedCurrencyProbe = false;
        loggedTrackedScan = false;
        loggedInventoryScanDebug.Clear();
    }

    private static void ScanRawInventory(
        NativeInventoryEntry entry,
        Dictionary<nint, InventoryEntityStack> entities,
        HashSet<nint> seenItems,
        bool debugLogging)
    {
        if (!Core.Process.ReadMemory(
                IntPtr.Add(entry.InventoryPtr, InventoryItemListOffset),
                out StdVector itemListVector))
        {
            return;
        }

        var itemSlots = Core.Process.ReadStdVector<nint>(itemListVector);
        var samplePaths = new List<string>(4);
        var currencySlots = 0;
        var nonZeroSlots = 0;
        nint firstSlot = IntPtr.Zero;

        foreach (var slot in itemSlots)
        {
            if (slot == IntPtr.Zero)
            {
                continue;
            }

            nonZeroSlots++;
            if (firstSlot == IntPtr.Zero)
            {
                firstSlot = slot;
            }

            if (!TryResolveItemEntity(slot, out var itemEntity) || !seenItems.Add(itemEntity))
            {
                continue;
            }

            var path = ReadItemPath(itemEntity);
            if (string.IsNullOrEmpty(path))
            {
                continue;
            }

            if (samplePaths.Count < 4)
            {
                samplePaths.Add(path);
            }

            if (!CurrencyPathMapper.IsTrackableCurrencyPath(path))
            {
                continue;
            }

            currencySlots++;
            var count = ItemStackCountReader.Read(itemEntity);
            entities[itemEntity] = new InventoryEntityStack(path, count);
        }

        if (entry.InventoryId == (int)InventoryName.Currency1 && !loggedCurrencyProbe)
        {
            loggedCurrencyProbe = true;
            Log.Info(
                $"raw Currency1: {itemSlots.Length} vector entries, {nonZeroSlots} non-zero slots, {currencySlots} currency stacks",
                "Better Loot Tracker");

            if (firstSlot != IntPtr.Zero)
            {
                Log.Info(
                    $"raw Currency1 first slot={firstSlot.ToInt64():X}",
                    "Better Loot Tracker");
            }

            if (samplePaths.Count > 0)
            {
                Log.Info(
                    $"raw Currency1 sample paths: {string.Join(" | ", samplePaths)}",
                    "Better Loot Tracker");
            }
        }

        if (debugLogging && currencySlots > 0 && loggedInventoryScanDebug.Add(entry.InventoryId))
        {
            Log.Info(
                $"raw inventory '{LabelFor(entry.InventoryId)}' has {currencySlots} currency stacks",
                "Better Loot Tracker");
        }
    }

    private static bool TryResolveItemEntity(nint slot, out nint itemEntity)
    {
        itemEntity = IntPtr.Zero;
        if (slot == IntPtr.Zero)
        {
            return false;
        }

        if (Core.Process.ReadMemory(IntPtr.Add(slot, InventoryItemSlotEntityOffset), out nint entityAtSlot) &&
            entityAtSlot != IntPtr.Zero &&
            !string.IsNullOrEmpty(ReadItemPath(entityAtSlot)))
        {
            itemEntity = entityAtSlot;
            return true;
        }

        if (!string.IsNullOrEmpty(ReadItemPath(slot)))
        {
            itemEntity = slot;
            return true;
        }

        if (Core.Process.ReadMemory(IntPtr.Add(slot, ItemEntityDetailsPtrOffset), out nint entityAtSlotPlus8) &&
            entityAtSlotPlus8 != IntPtr.Zero &&
            !string.IsNullOrEmpty(ReadItemPath(entityAtSlotPlus8)))
        {
            itemEntity = entityAtSlotPlus8;
            return true;
        }

        return false;
    }

    private static string ReadItemPath(nint itemEntity)
    {
        if (!Core.Process.ReadMemory(IntPtr.Add(itemEntity, ItemEntityDetailsPtrOffset), out nint detailsPtr) ||
            detailsPtr == IntPtr.Zero)
        {
            return string.Empty;
        }

        if (!Core.Process.ReadMemory(IntPtr.Add(detailsPtr, EntityDetailsNameOffset), out StdWString name))
        {
            return string.Empty;
        }

        var path = Core.Process.ReadStdWString(name);
        return string.IsNullOrEmpty(path) ? string.Empty : path;
    }

    private static string LabelFor(int inventoryId) =>
        Enum.IsDefined(typeof(InventoryName), inventoryId)
            ? ((InventoryName)inventoryId).ToString()
            : $"Inventory {inventoryId}";

    [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 24)]
    private struct InventoryEntryNative
    {
        [FieldOffset(0)]
        public int InventoryId;

        [FieldOffset(8)]
        public nint InventoryPtr0;
    }
}
