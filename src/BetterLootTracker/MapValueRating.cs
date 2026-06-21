namespace BetterLootTracker;

using System.Numerics;

internal static class MapValueRating
{
    public static bool TryGetRatingColor(
        BetterLootTrackerSettings settings,
        LootTrackerService tracker,
        double divineEquivalent,
        out Vector4 color)
    {
        color = default;
        if (!settings.ColorCodeMapValue ||
            !settings.ShowDivineEquivalent ||
            !tracker.HasPriceData)
        {
            return false;
        }

        var displayed = tracker.GetDisplayedValue(divineEquivalent, settings.ValueUnit);
        color = displayed >= settings.MapValueGoodThreshold
            ? settings.GoodMapValueColor
            : settings.BadMapValueColor;
        return true;
    }

    public static bool IsGood(
        BetterLootTrackerSettings settings,
        LootTrackerService tracker,
        double divineEquivalent)
    {
        if (!settings.ColorCodeMapValue ||
            !settings.ShowDivineEquivalent ||
            !tracker.HasPriceData)
        {
            return false;
        }

        var displayed = tracker.GetDisplayedValue(divineEquivalent, settings.ValueUnit);
        return displayed >= settings.MapValueGoodThreshold;
    }
}
