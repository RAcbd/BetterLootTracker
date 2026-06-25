namespace BetterLootTracker;

using OriathPlugins.Common.Pricing;

internal static class CurrencyFilter
{
    public static bool ShouldTrack(BetterLootTrackerSettings settings, string? priceId, string itemPath)
    {
        if (settings.TrackAllCurrencies)
        {
            return true;
        }

        if (settings.TrackAllLoot && CurrencyPathMapper.IsInventoryLootPath(itemPath))
        {
            return true;
        }

        if (!string.IsNullOrWhiteSpace(priceId) &&
            settings.TrackedCurrencyIds.Contains(priceId, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }

        return settings.TrackedCurrencyIds.Contains(itemPath, StringComparer.OrdinalIgnoreCase);
    }
}
