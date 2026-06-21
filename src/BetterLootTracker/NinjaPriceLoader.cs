namespace BetterLootTracker;

using Newtonsoft.Json.Linq;

internal sealed class NinjaPriceCatalog
{
    private readonly Dictionary<string, double> divineValuesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> displayNamesById = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> idsByPathHint = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> idsByDisplayName = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> idsByDetailsId = new(StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, string> IdAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        ["scroll"] = "wisdom",
    };

    public bool IsLoaded => divineValuesById.Count > 0;

    public static NinjaPriceCatalog Empty { get; } = new();

    public bool HasId(string? ninjaId)
    {
        if (string.IsNullOrWhiteSpace(ninjaId))
        {
            return false;
        }

        return divineValuesById.ContainsKey(ninjaId) ||
               (IdAliases.TryGetValue(ninjaId, out var alias) && divineValuesById.ContainsKey(alias));
    }

    public bool TryNormalizeId(string? ninjaId, out string? normalizedId)
    {
        normalizedId = null;
        if (string.IsNullOrWhiteSpace(ninjaId))
        {
            return false;
        }

        if (divineValuesById.ContainsKey(ninjaId))
        {
            normalizedId = ninjaId;
            return true;
        }

        if (IdAliases.TryGetValue(ninjaId, out var alias) && divineValuesById.ContainsKey(alias))
        {
            normalizedId = alias;
            return true;
        }

        return false;
    }

    public bool TryGetDivineValue(string? ninjaId, out double value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(ninjaId))
        {
            return false;
        }

        if (divineValuesById.TryGetValue(ninjaId, out value))
        {
            return true;
        }

        return IdAliases.TryGetValue(ninjaId, out var alias) &&
               divineValuesById.TryGetValue(alias, out value);
    }

    public string? TryGetDisplayName(string? ninjaId) =>
        string.IsNullOrWhiteSpace(ninjaId) ? null :
        displayNamesById.TryGetValue(ninjaId, out var name) ? name : null;

    public bool TryGetIdForDisplayName(string? displayName, out string? ninjaId)
    {
        ninjaId = null;
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return false;
        }

        if (idsByDisplayName.TryGetValue(displayName, out var exactMatch))
        {
            ninjaId = exactMatch;
            return true;
        }

        var normalized = NormalizeDisplayName(displayName);
        return !string.IsNullOrWhiteSpace(normalized) &&
               idsByDisplayName.TryGetValue(normalized, out ninjaId);
    }

    public bool TryGetIdForPath(string itemPath, out string? ninjaId)
    {
        ninjaId = null;
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        var fileName = itemPath.Replace('\\', '/').Split('/').LastOrDefault();
        if (string.IsNullOrEmpty(fileName))
        {
            return false;
        }

        if (idsByPathHint.TryGetValue(fileName, out var mapped))
        {
            ninjaId = mapped;
            return true;
        }

        return false;
    }

    public IReadOnlyList<CurrencyOption> GetCurrencyOptions()
    {
        var ids = new HashSet<string>(divineValuesById.Keys, StringComparer.OrdinalIgnoreCase);
        foreach (var id in displayNamesById.Keys)
        {
            ids.Add(id);
        }

        return ids
            .Select(id => new CurrencyOption(
                id,
                displayNamesById.TryGetValue(id, out var name) ? name : id,
                divineValuesById.GetValueOrDefault(id)))
            .OrderBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public void MergeFromFile(string filePath, out string? error)
    {
        error = null;
        if (!File.Exists(filePath))
        {
            error = $"Price file not found: {filePath}";
            return;
        }

        try
        {
            var root = JObject.Parse(File.ReadAllText(filePath));

            if (root["core"]?["items"] is JArray coreItems)
            {
                IndexItemMetadata(coreItems);
            }

            if (root["items"] is JArray items)
            {
                IndexItemMetadata(items);
            }

            if (root["LinesByName"] is JObject linesByName)
            {
                IndexLinesByName(linesByName);
            }

            if (root["lines"] is JArray lines)
            {
                foreach (var line in lines.OfType<JObject>())
                {
                    var id = line["id"]?.ToString();
                    var primaryValue = line["primaryValue"]?.Value<double?>();
                    if (string.IsNullOrWhiteSpace(id) || primaryValue is null or <= 0)
                    {
                        continue;
                    }

                    divineValuesById[id] = primaryValue.Value;
                }
            }
        }
        catch (Exception ex)
        {
            error = ex.Message;
        }
    }

    private void IndexItemMetadata(JArray items)
    {
        foreach (var item in items.OfType<JObject>())
        {
            var id = item["id"]?.ToString();
            var name = item["name"]?.ToString();
            var detailsId = item["detailsId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
            {
                displayNamesById[id] = name;
                idsByDisplayName[name] = id;
                idsByDisplayName[NormalizeDisplayName(name)] = id;
            }

            if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(detailsId))
            {
                idsByDetailsId[detailsId] = id;
            }

            IndexPathHint(item["image"]?.ToString(), id);
        }
    }

    private void IndexLinesByName(JObject linesByName)
    {
        foreach (var property in linesByName.Properties())
        {
            var displayName = property.Name;
            if (string.IsNullOrWhiteSpace(displayName) || property.Value is not JObject entry)
            {
                continue;
            }

            var itemMeta = entry["Item2"] as JObject ?? entry["Item1"] as JObject;
            var id = itemMeta?["id"]?.ToString();
            if (string.IsNullOrWhiteSpace(id))
            {
                continue;
            }

            displayNamesById[id] = displayName;
            idsByDisplayName[displayName] = id;
            idsByDisplayName[NormalizeDisplayName(displayName)] = id;

            var detailsId = itemMeta?["detailsId"]?.ToString();
            if (!string.IsNullOrWhiteSpace(detailsId))
            {
                idsByDetailsId[detailsId] = id;
            }

            IndexPathHint(itemMeta?["image"]?.ToString(), id);
        }
    }

    private void IndexPathHint(string? image, string? id)
    {
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(image))
        {
            return;
        }

        var marker = "2DItems/";
        var markerIndex = image.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (markerIndex < 0)
        {
            return;
        }

        var pathHint = image[(markerIndex + marker.Length)..];
        var slash = pathHint.IndexOf('/');
        if (slash >= 0)
        {
            pathHint = pathHint[(slash + 1)..];
        }

        var dot = pathHint.IndexOf('.');
        if (dot >= 0)
        {
            pathHint = pathHint[..dot];
        }

        if (!string.IsNullOrWhiteSpace(pathHint))
        {
            idsByPathHint[pathHint] = id;
        }
    }

    private static string NormalizeDisplayName(string name) =>
        name.Replace("'", string.Empty, StringComparison.Ordinal)
            .Replace("’", string.Empty, StringComparison.Ordinal)
            .Trim();
}

internal static class NinjaPriceLoader
{
    public static string GetLeagueDataDirectory(string pluginsRoot, string league) =>
        Path.Combine(pluginsRoot, "NinjaPricer", "ninja-data", league);

    public static string GetPluginsRootFromDllDirectory(string dllDirectory)
    {
        var pluginsDir = Directory.GetParent(dllDirectory)?.FullName;
        return pluginsDir ?? dllDirectory;
    }

    public static bool TryLoadLeagueCatalog(string leagueDataDirectory, out NinjaPriceCatalog catalog, out string status)
    {
        catalog = new NinjaPriceCatalog();
        if (!Directory.Exists(leagueDataDirectory))
        {
            status = $"Ninja data directory not found: {leagueDataDirectory}";
            catalog = NinjaPriceCatalog.Empty;
            return false;
        }

        var loadedFiles = 0;
        var errors = new List<string>();

        foreach (var filePath in Directory.EnumerateFiles(leagueDataDirectory, "*.json"))
        {
            var fileName = Path.GetFileName(filePath);
            if (fileName.Equals("meta.json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            catalog.MergeFromFile(filePath, out var error);
            if (error is null)
            {
                loadedFiles++;
            }
            else
            {
                errors.Add($"{fileName}: {error}");
            }
        }

        if (!catalog.IsLoaded)
        {
            status = errors.Count > 0
                ? string.Join("; ", errors)
                : $"No Ninja price files found in {leagueDataDirectory}";
            catalog = NinjaPriceCatalog.Empty;
            return false;
        }

        status = loadedFiles == 1
            ? $"Loaded prices from {leagueDataDirectory}"
            : $"Loaded {loadedFiles} Ninja price files from {leagueDataDirectory}";

        if (errors.Count > 0)
        {
            status += $" ({errors.Count} file warnings)";
        }

        return true;
    }
}
