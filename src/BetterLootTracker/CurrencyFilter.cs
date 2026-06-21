namespace BetterLootTracker;

public readonly record struct CurrencyOption(string Id, string Name, double DivineValue);

internal static class CurrencyFilter
{
    public static bool ShouldTrack(BetterLootTrackerSettings settings, string? priceId, string itemPath)
    {
        if (settings.TrackAllCurrencies)
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
