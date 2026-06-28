namespace OriathPlugins.Common.Pricing;

using OriathHub;
using OriathHub.RemoteObjects.FilesStructures;

public static class BaseItemTypeResolver
{
    private const string DatPath = "Data/Balance/BaseItemTypes.dat";
    private const int RowSize = 360;
    private const int MetadataPathOffset = 0;
    private const int BaseNameOffset = 32;

    private static readonly Dictionary<string, string> BaseNameByPath = new(StringComparer.Ordinal);
    private static bool loaded;

    public static bool TryResolveBaseName(string itemPath, out string baseName)
    {
        baseName = string.Empty;
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        EnsureLoaded();
        return BaseNameByPath.TryGetValue(itemPath, out baseName!) &&
               !string.IsNullOrWhiteSpace(baseName);
    }

    public static void Invalidate()
    {
        loaded = false;
        BaseNameByPath.Clear();
    }

    private static void EnsureLoaded()
    {
        if (loaded)
        {
            return;
        }

        loaded = true;
        if (!DatFileReader.TryGetDatTable(DatPath, out var table) || !table.IsValid)
        {
            return;
        }

        var rowCount = table.RowCount(RowSize);
        for (var i = 0; i < rowCount; i++)
        {
            var row = table.Row(i, RowSize);
            var metadataPath = ReadStringColumn(row + MetadataPathOffset);
            if (string.IsNullOrEmpty(metadataPath))
            {
                continue;
            }

            var name = ReadStringColumn(row + BaseNameOffset);
            if (!string.IsNullOrWhiteSpace(name))
            {
                BaseNameByPath[metadataPath] = name;
            }
        }
    }

    private static string ReadStringColumn(nint columnAddress)
    {
        if (!Core.Process.ReadMemory(columnAddress, out nint stringPtr) || stringPtr == IntPtr.Zero)
        {
            return string.Empty;
        }

        return Core.Process.ReadUnicodeString(stringPtr);
    }
}
