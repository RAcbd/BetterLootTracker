namespace OriathPlugins.Common.Pricing;

using OriathHub;
using OriathHub.Pricing;
using OriathHub.RemoteEnums;
using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Inventory;
using OriathPlugins.Common.Loot;

public static class HostPriceHelper
{
    private const string DivinePath = "Metadata/Items/Currency/CurrencyModValues/DivineOrb";
    private const string ChaosPath = "Metadata/Items/Currency/CurrencyModValues/CurrencyModValues";

    public static string League => Core.Prices.League;

    public static bool HasPriceData => Core.Prices.GetStatus(League).HasData;

    public static string GetStatusMessage()
    {
        var status = Core.Prices.GetStatus(League);
        if (status.IsLoading)
        {
            return $"Loading prices for {League}…";
        }

        if (status.HasData)
        {
            return $"Prices loaded ({status.UpdatedUtc:u}) — {League}";
        }

        return string.IsNullOrWhiteSpace(status.Error)
            ? "Prices not loaded."
            : status.Error;
    }

    public static void RequestRefresh() => Core.Prices.RequestRefresh(League);

    public static double GetDivineToExaltedRate()
    {
        var rate = Core.Prices.GetDivineToExaltedRate(League);
        return rate > 0 ? rate : 250d;
    }

    public static double ExaltedToDivine(double exaltedValue)
    {
        var rate = Core.Prices.GetDivineToExaltedRate(League);
        return rate > 0 ? exaltedValue / rate : 0d;
    }

    public static bool TryGetQuote(Item item, out ItemPrice quote) =>
        Core.Prices.TryGetPrice(item, League, out quote);

    public static bool TryGetQuote(
        string path,
        Rarity rarity,
        string artPath,
        int stackCount,
        out ItemPrice quote)
    {
        var query = new PriceQuery(path, rarity, artPath ?? string.Empty, Math.Max(1, stackCount), 0);
        return Core.Prices.TryGetPrice(in query, League, out quote);
    }

    public static bool TryGetQuote(Entity entity, out ItemPrice quote)
    {
        quote = default;
        if (entity is Item item)
        {
            return TryGetQuote(item, out quote);
        }

        if (!TryBuildQuery(entity, out var query))
        {
            return false;
        }

        return Core.Prices.TryGetPrice(in query, League, out quote);
    }

    public static bool TryGetDivineUnitValue(Item item, out double divineValue, out string displayName)
    {
        divineValue = 0;
        displayName = string.Empty;
        if (!TryGetQuote(item, out var quote))
        {
            return false;
        }

        var stackCount = ReadStackCount(item);
        displayName = quote.DisplayName;
        divineValue = ExaltedToDivine(quote.ExaltedValue / stackCount);
        return divineValue > 0;
    }

    public static bool TryGetDivineUnitValue(Entity entity, out double divineValue, out string displayName)
    {
        divineValue = 0;
        displayName = string.Empty;
        if (!TryGetQuote(entity, out var quote))
        {
            return false;
        }

        var stackCount = ItemStackCountReader.Read(entity);
        displayName = quote.DisplayName;
        divineValue = ExaltedToDivine(quote.ExaltedValue / Math.Max(1, stackCount));
        return divineValue > 0;
    }

    public static bool TryGetDivineUnitValueForPath(
        string itemPath,
        int stackCount,
        out double divineValue,
        out string displayName)
    {
        divineValue = 0;
        displayName = string.Empty;
        if (string.IsNullOrWhiteSpace(itemPath))
        {
            return false;
        }

        if (!TryGetQuote(itemPath, Rarity.Normal, string.Empty, stackCount, out var quote))
        {
            return false;
        }

        displayName = quote.DisplayName;
        divineValue = ExaltedToDivine(quote.ExaltedValue / Math.Max(1, stackCount));
        return divineValue > 0;
    }

    public static double GetChaosPerDivine()
    {
        if (TryGetQuote(ChaosPath, Rarity.Normal, string.Empty, 1, out var chaos) &&
            TryGetQuote(DivinePath, Rarity.Normal, string.Empty, 1, out var divine) &&
            chaos.ExaltedValue > 0)
        {
            return divine.ExaltedValue / chaos.ExaltedValue;
        }

        return 10d;
    }

    public static double GetDisplayedValue(double divineValue, string unit) =>
        unit.ToLowerInvariant() switch
        {
            "chaos" => divineValue * GetChaosPerDivine(),
            "exalted" => divineValue * GetDivineToExaltedRate(),
            _ => divineValue,
        };

    public static string GetValueSuffix(string unit) =>
        unit.ToLowerInvariant() switch
        {
            "chaos" => "c",
            "exalted" => "ex",
            _ => "d",
        };

    public static IReadOnlyList<CurrencyOption> GetCurrencyOptions()
    {
        var options = new List<CurrencyOption>();
        foreach (var (path, id) in CurrencyPathMapper.GetCurrencyOptionEntries())
        {
            if (!TryGetQuote(path, Rarity.Normal, string.Empty, 1, out var quote))
            {
                options.Add(new CurrencyOption(id, CurrencyPathMapper.GetDisplayName(path, null), 0));
                continue;
            }

            var divineValue = ExaltedToDivine(quote.ExaltedValue);
            options.Add(new CurrencyOption(id, quote.DisplayName, divineValue));
        }

        return options
            .OrderByDescending(static option => option.DivineValue)
            .ThenBy(static option => option.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryBuildQuery(Entity entity, out PriceQuery query)
    {
        query = default;
        var path = GroundLootRules.ResolveItemPath(entity);
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var rarity = Rarity.Normal;
        if (entity.TryGetComponent<ObjectMagicProperties>(out var magic))
        {
            rarity = magic.Rarity;
        }

        var artPath = string.Empty;
        if (entity.TryGetComponent<RenderItem>(out var render) &&
            !string.IsNullOrWhiteSpace(render.ResourcePath))
        {
            artPath = render.ResourcePath;
        }

        var stackCount = ItemStackCountReader.Read(entity);
        query = new PriceQuery(path, rarity, artPath, Math.Max(1, stackCount), 0);
        return true;
    }

    private static int ReadStackCount(Item item) =>
        item.TryGetComponent<Stack>(out var stack) && stack.Count > 0 ? stack.Count : 1;
}
