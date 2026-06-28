namespace OriathPlugins.Common.Pricing;

public static class PriceResolver
{
    public static string? Resolve(string itemPath, string? displayName, CurrencyDisplayNameStore names)
    {
        if (names.TryResolveNinjaId(itemPath, out var configuredId) &&
            !string.IsNullOrWhiteSpace(configuredId))
        {
            return configuredId;
        }

        return CurrencyPathMapper.TryMapToNinjaId(itemPath);
    }

    public static string ResolveDisplayName(
        string itemPath,
        string? priceId,
        string? fallbackName,
        CurrencyDisplayNameStore names)
    {
        if (HostPriceHelper.TryGetDivineUnitValueForPath(itemPath, 1, out _, out var pricedName) &&
            !string.IsNullOrWhiteSpace(pricedName))
        {
            return pricedName;
        }

        var fromStore = names.Resolve(itemPath, priceId, fallbackName);
        if (!string.IsNullOrWhiteSpace(fromStore) &&
            !string.Equals(fromStore, GetFileName(itemPath), StringComparison.OrdinalIgnoreCase))
        {
            return fromStore;
        }

        if (BaseItemTypeResolver.TryResolveBaseName(itemPath, out var baseName))
        {
            return baseName;
        }

        return names.Resolve(itemPath, priceId, fallbackName);
    }

    private static string GetFileName(string itemPath) =>
        itemPath.Replace('\\', '/').Split('/').LastOrDefault() ?? itemPath;
}
