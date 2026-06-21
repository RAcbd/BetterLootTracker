namespace BetterLootTracker;

internal static class PriceResolver
{
    public static string? Resolve(
        string itemPath,
        string? displayName,
        NinjaPriceCatalog catalog,
        CurrencyDisplayNameStore displayNames)
    {
        if (displayNames.TryResolveNinjaId(itemPath, out var configuredId) &&
            catalog.TryNormalizeId(configuredId, out var normalizedConfigured))
        {
            return normalizedConfigured;
        }

        if (catalog.TryGetIdForPath(itemPath, out var pathHintId) &&
            catalog.TryNormalizeId(pathHintId, out var normalizedPathHint))
        {
            return normalizedPathHint;
        }

        var pathMapped = CurrencyPathMapper.TryMapToNinjaId(itemPath, catalog);
        if (catalog.TryNormalizeId(pathMapped, out var normalizedPathMapped))
        {
            return normalizedPathMapped;
        }

        if (BaseItemTypeResolver.TryResolveBaseName(itemPath, out var baseName) &&
            catalog.TryGetIdForDisplayName(baseName, out var baseNameId))
        {
            return baseNameId;
        }

        var catalogDisplayName = catalog.TryGetDisplayName(pathMapped);
        if (!string.IsNullOrWhiteSpace(catalogDisplayName) &&
            catalog.TryGetIdForDisplayName(catalogDisplayName, out var catalogNameId))
        {
            return catalogNameId;
        }

        if (!string.IsNullOrWhiteSpace(displayName) &&
            catalog.TryGetIdForDisplayName(displayName, out var displayMapped))
        {
            return displayMapped;
        }

        return null;
    }

    public static string ResolveDisplayName(
        string itemPath,
        string? priceId,
        string? fallbackName,
        CurrencyDisplayNameStore displayNames,
        NinjaPriceCatalog catalog)
    {
        var fromStore = displayNames.Resolve(itemPath, priceId, null);
        if (!string.IsNullOrWhiteSpace(fromStore) &&
            !string.Equals(fromStore, GetFileName(itemPath), StringComparison.OrdinalIgnoreCase) &&
            !LooksLikeHumanizedPath(fromStore))
        {
            return fromStore;
        }

        var catalogName = catalog.TryGetDisplayName(priceId);
        if (!string.IsNullOrWhiteSpace(catalogName))
        {
            return catalogName;
        }

        if (BaseItemTypeResolver.TryResolveBaseName(itemPath, out var baseName))
        {
            return baseName;
        }

        return displayNames.Resolve(itemPath, priceId, fallbackName);
    }

    private static bool LooksLikeHumanizedPath(string value) =>
        value.Contains(' ') && !value.Contains('\'');

    private static string GetFileName(string itemPath) =>
        itemPath.Replace('\\', '/').Split('/').LastOrDefault() ?? itemPath;
}
