namespace BetterLootTracker;

using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States.InGameStateObjects;

internal static class ItemStackCountReader
{
    public static int Read(Item item)
    {
        if (!item.IsValid)
        {
            return 1;
        }

        var hostCount = 0;
        if (item.TryGetComponent<Stack>(out var stack) && stack.Count > 0)
        {
            hostCount = stack.Count;
        }

        var nativeCount = NativeItemComponentReader.ReadStackCount(item.Address);
        return Math.Max(hostCount, nativeCount);
    }

    public static int Read(nint itemAddress) =>
        itemAddress == IntPtr.Zero ? 1 : NativeItemComponentReader.ReadStackCount(itemAddress);
}
