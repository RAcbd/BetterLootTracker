namespace BetterLootTracker;

using System.Runtime.InteropServices;
using GameOffsets.Natives;
using OriathHub;
using OriathHub.Utils;

internal static class NativeItemComponentReader
{
    private const int EntityDetailsPtrOffset = 0x8;
    private const int ComponentListOffset = 0x10;
    private const int ComponentLookUpPtrOffset = 0x28;
    private const int ComponentsBucketOffset = 0x28;
    private const int StackCountOffset = 0x18;
    private const int MaxComponentsInEntity = 50;

    public static int ReadStackCount(nint itemEntity)
    {
        if (itemEntity == IntPtr.Zero)
        {
            return 1;
        }

        if (!TryResolveComponentAddresses(itemEntity).TryGetValue("Stack", out var stackComponent) ||
            stackComponent == IntPtr.Zero)
        {
            return 1;
        }

        if (Core.Process.ReadMemory(IntPtr.Add(stackComponent, StackCountOffset), out int stackCount) &&
            stackCount > 0)
        {
            return stackCount;
        }

        return 1;
    }

    private static Dictionary<string, nint> TryResolveComponentAddresses(nint entityAddress)
    {
        var components = new Dictionary<string, nint>(StringComparer.Ordinal);
        if (entityAddress == IntPtr.Zero)
        {
            return components;
        }

        if (!Core.Process.ReadMemory(IntPtr.Add(entityAddress, EntityDetailsPtrOffset), out nint detailsPtr) ||
            detailsPtr == IntPtr.Zero)
        {
            return components;
        }

        if (!Core.Process.ReadMemory(IntPtr.Add(entityAddress, ComponentListOffset), out StdVector componentList))
        {
            return components;
        }

        var componentAddresses = Core.Process.ReadStdVector<nint>(componentList);
        if (componentAddresses.Length == 0)
        {
            return components;
        }

        if (!Core.Process.ReadMemory(IntPtr.Add(detailsPtr, ComponentLookUpPtrOffset), out nint lookupPtr) ||
            lookupPtr == IntPtr.Zero)
        {
            return components;
        }

        if (!Core.Process.ReadMemory(IntPtr.Add(lookupPtr, ComponentsBucketOffset), out StdBucket bucket) ||
            bucket.Capacity is <= 0 or > MaxComponentsInEntity)
        {
            return components;
        }

        var names = Core.Process.ReadStdVector<ComponentNameAndIndex>(bucket.Data);
        foreach (var nameEntry in names)
        {
            if (nameEntry.Index < 0 || nameEntry.Index >= componentAddresses.Length)
            {
                continue;
            }

            var componentName = ReadAsciiName(nameEntry.NamePtr);
            if (string.IsNullOrEmpty(componentName))
            {
                continue;
            }

            components[componentName] = componentAddresses[nameEntry.Index];
        }

        return components;
    }

    private static string ReadAsciiName(nint namePtr)
    {
        if (namePtr == IntPtr.Zero ||
            !Core.Process.ReadMemoryArray(namePtr, 32, out byte[] bytes) ||
            bytes.Length == 0)
        {
            return string.Empty;
        }

        var terminator = Array.IndexOf(bytes, (byte)0);
        if (terminator <= 0)
        {
            return string.Empty;
        }

        return System.Text.Encoding.ASCII.GetString(bytes, 0, terminator);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct ComponentNameAndIndex
    {
        public nint NamePtr;
        public int Index;
        public int Pad0xC;
    }
}
