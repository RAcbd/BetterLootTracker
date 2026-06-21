namespace BetterLootTracker;

using Newtonsoft.Json;
using OriathHub.Utils;

public sealed class CurrencyDisplayNameStore
{
    private readonly Dictionary<string, string> byPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> byNinjaId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> byPathNinjaId = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> discoveryWriteAttempted = new(StringComparer.OrdinalIgnoreCase);

    private string dataFilePath = string.Empty;

    public void Load(string dllDirectory)
    {
        byPath.Clear();
        byNinjaId.Clear();
        byPathNinjaId.Clear();
        discoveryWriteAttempted.Clear();

        var dataDir = Path.Combine(dllDirectory, "data");
        Directory.CreateDirectory(dataDir);
        dataFilePath = Path.Combine(dataDir, "currency-names.json");

        if (!File.Exists(dataFilePath))
        {
            WriteDefaultFile(dataFilePath);
        }

        try
        {
            var root = JsonConvert.DeserializeObject<CurrencyNamesFile>(File.ReadAllText(dataFilePath));
            if (root?.ByPath is not null)
            {
                foreach (var (key, value) in root.ByPath)
                {
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        byPath[key] = value;
                    }
                }
            }

            if (root?.ByNinjaId is not null)
            {
                foreach (var (key, value) in root.ByNinjaId)
                {
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        byNinjaId[key] = value;
                    }
                }
            }

            if (root?.ByPathNinjaId is not null)
            {
                foreach (var (key, value) in root.ByPathNinjaId)
                {
                    if (!string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(value))
                    {
                        byPathNinjaId[key] = value;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to load currency names: {ex.Message}", "Better Loot Tracker");
        }
    }

    public string Resolve(string itemPath, string? priceId, string? fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(itemPath) && byPath.TryGetValue(itemPath, out var byFullPath))
        {
            return byFullPath;
        }

        var fileName = GetFileName(itemPath);
        if (!string.IsNullOrWhiteSpace(fileName) && byPath.TryGetValue(fileName, out var byFileName))
        {
            return byFileName;
        }

        if (!string.IsNullOrWhiteSpace(priceId) && byNinjaId.TryGetValue(priceId, out var byId))
        {
            return byId;
        }

        return string.IsNullOrWhiteSpace(fallbackName) ? fileName : fallbackName;
    }

    public bool TryResolveNinjaId(string itemPath, out string? ninjaId)
    {
        ninjaId = null;
        if (!string.IsNullOrWhiteSpace(itemPath) && byPathNinjaId.TryGetValue(itemPath, out var byFullPath))
        {
            ninjaId = byFullPath;
            return true;
        }

        var fileName = GetFileName(itemPath);
        return !string.IsNullOrWhiteSpace(fileName) &&
               byPathNinjaId.TryGetValue(fileName, out ninjaId);
    }

    internal void TryRegisterDiscovery(
        string itemPath,
        string? priceId,
        string displayName,
        NinjaPriceCatalog catalog)
    {
        var fileName = GetFileName(itemPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        if (catalog.TryNormalizeId(priceId, out var normalizedId) && !string.IsNullOrWhiteSpace(normalizedId))
        {
            TryAppendPathNinjaId(fileName, normalizedId);
            byPathNinjaId[fileName] = normalizedId;

            var ninjaName = catalog.TryGetDisplayName(normalizedId);
            if (!string.IsNullOrWhiteSpace(ninjaName))
            {
                displayName = ninjaName;
            }
        }

        if (HasPathOverride(itemPath, fileName))
        {
            return;
        }

        if (!discoveryWriteAttempted.Add(fileName))
        {
            return;
        }

        if (TryAppendPathOverride(fileName, displayName))
        {
            byPath[fileName] = displayName;
            Log.Info($"added currency name entry: {fileName}", "Better Loot Tracker");
        }
    }

    public string DataFilePath => dataFilePath;

    private bool HasPathOverride(string itemPath, string fileName) =>
        byPath.ContainsKey(fileName) ||
        (!string.IsNullOrWhiteSpace(itemPath) && byPath.ContainsKey(itemPath));

    private bool TryAppendPathNinjaId(string key, string ninjaId)
    {
        if (string.IsNullOrWhiteSpace(dataFilePath))
        {
            return false;
        }

        try
        {
            var root = File.Exists(dataFilePath)
                ? JsonConvert.DeserializeObject<CurrencyNamesFile>(File.ReadAllText(dataFilePath)) ?? new CurrencyNamesFile()
                : new CurrencyNamesFile();

            root.ByPathNinjaId ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.ByPathNinjaId.ContainsKey(key))
            {
                return false;
            }

            root.ByPathNinjaId[key] = ninjaId;
            File.WriteAllText(dataFilePath, JsonConvert.SerializeObject(root, Formatting.Indented));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to update currency names: {ex.Message}", "Better Loot Tracker");
            return false;
        }
    }

    private bool TryAppendPathOverride(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(dataFilePath))
        {
            return false;
        }

        try
        {
            var root = File.Exists(dataFilePath)
                ? JsonConvert.DeserializeObject<CurrencyNamesFile>(File.ReadAllText(dataFilePath)) ?? new CurrencyNamesFile()
                : new CurrencyNamesFile();

            root.ByPath ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (root.ByPath.ContainsKey(key))
            {
                return false;
            }

            root.ByPath[key] = value;
            File.WriteAllText(dataFilePath, JsonConvert.SerializeObject(root, Formatting.Indented));
            return true;
        }
        catch (Exception ex)
        {
            Log.Error($"Failed to update currency names: {ex.Message}", "Better Loot Tracker");
            return false;
        }
    }

    private static string GetFileName(string itemPath) =>
        itemPath.Replace('\\', '/').Split('/').LastOrDefault() ?? itemPath;

    private static void WriteDefaultFile(string path)
    {
        var defaults = new CurrencyNamesFile
        {
            ByPath = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CurrencyAddModToMagic"] = "Orb of Augmentation",
                ["CurrencyAddModToMagic2"] = "Greater Orb of Augmentation",
                ["CurrencyAddModToMagic3"] = "Perfect Orb of Augmentation",
                ["CurrencyCorrupt"] = "Vaal Orb",
                ["CurrencyVaal"] = "Vaal Orb",
                ["CurrencyRerollRare"] = "Chaos Orb",
                ["CurrencyModValues"] = "Divine Orb",
                ["CurrencyVerisiumMetal1"] = "Verisium",
                ["CurrencyAddModToRare"] = "Exalted Orb",
                ["CurrencyAddModToRare2"] = "Greater Exalted Orb",
                ["CurrencyAddModToRare3"] = "Perfect Exalted Orb",
                ["CurrencyRerollRare2"] = "Greater Chaos Orb",
                ["CurrencyRerollRare3"] = "Perfect Chaos Orb",
                ["CurrencyBreachShard"] = "Breach Splinter",
                ["AbyssalBenchTicketWeapon"] = "Preserved Jawbone",
                ["AbyssalBenchTicketJewel"] = "Preserved Cranium",
            },
            ByPathNinjaId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["CurrencyAddModToRare2"] = "greater-exalted-orb",
                ["AbyssalBenchTicketJewel"] = "preserved-cranium",
            },
            ByNinjaId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["aug"] = "Orb of Augmentation",
                ["vaal"] = "Vaal Orb",
                ["chaos"] = "Chaos Orb",
                ["divine"] = "Divine Orb",
            },
        };

        File.WriteAllText(path, JsonConvert.SerializeObject(defaults, Formatting.Indented));
    }

    private sealed class CurrencyNamesFile
    {
        [JsonProperty("byPath")]
        public Dictionary<string, string>? ByPath { get; set; }

        [JsonProperty("byNinjaId")]
        public Dictionary<string, string>? ByNinjaId { get; set; }

        [JsonProperty("byPathNinjaId")]
        public Dictionary<string, string>? ByPathNinjaId { get; set; }
    }
}
