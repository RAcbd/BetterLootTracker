namespace OriathPlugins.Common.Pricing;

using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Loot;

public sealed class LootPriceService
{
    private readonly CurrencyDisplayNameStore displayNames = new();

    public bool HasPriceData => HostPriceHelper.HasPriceData;

    public string StatusMessage => HostPriceHelper.GetStatusMessage();

    public CurrencyDisplayNameStore DisplayNames => displayNames;

    public void Initialize(string dllDirectory) => displayNames.Load(dllDirectory);

    public void RefreshPrices() => HostPriceHelper.RequestRefresh();

    public bool TryEvaluateGroundLoot(Entity entity, out double divineValue, out string? priceId)
    {
        divineValue = 0;
        priceId = null;
        var itemPath = GroundLootRules.ResolveItemPath(entity);
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        if (!HostPriceHelper.TryGetDivineUnitValue(entity, out divineValue, out var displayName))
        {
            return false;
        }

        priceId = CurrencyPathMapper.TryMapToNinjaId(itemPath) ?? itemPath;
        displayNames.Resolve(itemPath, priceId, displayName);
        return true;
    }

    public int GetPickupPriority(string? itemPath, double divineValue, bool alwaysPickupWaystonesAndTablets)
    {
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return 100;
        }

        if (alwaysPickupWaystonesAndTablets && LootPathMatcher.IsAlwaysPickupPath(itemPath))
        {
            return 0;
        }

        if (itemPath.Contains("CurrencyModValues", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(LootPathMatcher.GetNinjaIdHint(itemPath), "divine", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (divineValue >= 1d)
        {
            return 2;
        }

        if (divineValue > 0d)
        {
            return 3;
        }

        return 10;
    }

    public bool TryGetDivineValueForPath(string itemPath, out double divineValue)
    {
        divineValue = 0;
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        if (!HostPriceHelper.TryGetDivineUnitValueForPath(itemPath, 1, out divineValue, out _))
        {
            return false;
        }

        return divineValue > 0;
    }

    public IReadOnlyList<CurrencyOption> GetCurrencyOptions() => HostPriceHelper.GetCurrencyOptions();

    public double GetDisplayedValue(double divineValue, string unit) =>
        HostPriceHelper.GetDisplayedValue(divineValue, unit);

    public string GetValueSuffix(string unit) => HostPriceHelper.GetValueSuffix(unit);

    public string GetValueUnitLabel(string unit) =>
        unit.ToLowerInvariant() switch
        {
            "chaos" => "Chaos",
            "exalted" => "Exalted",
            _ => "Divine",
        };
}
