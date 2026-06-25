namespace BetterLootTracker;

using System.Numerics;
using ImGuiNET;
using OriathHub.Utils;

internal enum HudLineType
{
    Plain,
    Spacer,
    CurrencyHeader,
    CurrencyRow,
}

internal readonly record struct HudLine(
    HudLineType Type,
    string Text = "",
    bool IsHeader = false,
    bool IsMuted = false,
    string Item = "",
    string Qty = "",
    string Price = "",
    Vector4? TextColor = null);

internal static class LootHudRenderer
{
    private const float ColumnGap = 12f;

    public static void Draw(
        BetterLootTrackerSettings settings,
        SessionLootState state,
        LootTrackerService tracker)
    {
        var lines = BuildLines(settings, state, tracker);
        if (lines.Count == 0)
        {
            return;
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var font = ImGui.GetFont();
        var fontSize = Math.Clamp(settings.HudFontSize, 10f, 32f);
        var padding = 8f;
        var lineHeight = fontSize + 4f;
        var columns = ComputeColumns(lines, font, fontSize);
        var maxWidth = ComputeMaxWidth(lines, font, fontSize, columns, padding);
        var boxSize = new Vector2(maxWidth + padding * 2f, lines.Count * lineHeight + padding * 2f);
        var topLeft = settings.DrawPosition;
        var bottomRight = topLeft + boxSize;

        drawList.AddRectFilled(
            topLeft,
            bottomRight,
            ImGuiHelper.Color(settings.DefaultBackgroundColor));

        var textColor = ImGuiHelper.Color(settings.DefaultFontColor);
        var headerColor = ImGuiHelper.Color(settings.AccentColor);
        var mutedColor = ImGuiHelper.Color(new Vector4(
            settings.DefaultFontColor.X * 0.55f,
            settings.DefaultFontColor.Y * 0.55f,
            settings.DefaultFontColor.Z * 0.55f,
            settings.DefaultFontColor.W));

        var y = topLeft.Y + padding;
        foreach (var line in lines)
        {
            if (line.Type == HudLineType.Spacer)
            {
                y += lineHeight * 0.35f;
                continue;
            }

            var color = line.TextColor is { } customColor
                ? ImGuiHelper.Color(customColor)
                : line.IsHeader ? headerColor : line.IsMuted ? mutedColor : textColor;
            var x = topLeft.X + padding;

            if (line.Type is HudLineType.CurrencyHeader or HudLineType.CurrencyRow)
            {
                drawList.AddText(font, fontSize, new Vector2(x, y), color, line.Item);
                drawList.AddText(font, fontSize, new Vector2(x + columns.ItemWidth + ColumnGap, y), color, line.Qty);
                drawList.AddText(font, fontSize, new Vector2(x + columns.ItemWidth + ColumnGap + columns.QtyWidth + ColumnGap, y), color, line.Price);
            }
            else
            {
                drawList.AddText(font, fontSize, new Vector2(x, y), color, line.Text);
            }

            y += lineHeight;
        }
    }

    private readonly record struct ColumnLayout(float ItemWidth, float QtyWidth, float PriceWidth);

    private static ColumnLayout ComputeColumns(IReadOnlyList<HudLine> lines, ImFontPtr font, float fontSize)
    {
        var itemWidth = MeasureTextWidth(font, fontSize, "Item");
        var qtyWidth = MeasureTextWidth(font, fontSize, "Qty");
        var priceWidth = MeasureTextWidth(font, fontSize, "Price");

        foreach (var line in lines)
        {
            if (line.Type is not (HudLineType.CurrencyHeader or HudLineType.CurrencyRow))
            {
                continue;
            }

            itemWidth = Math.Max(itemWidth, MeasureTextWidth(font, fontSize, line.Item));
            qtyWidth = Math.Max(qtyWidth, MeasureTextWidth(font, fontSize, line.Qty));
            priceWidth = Math.Max(priceWidth, MeasureTextWidth(font, fontSize, line.Price));
        }

        return new ColumnLayout(itemWidth, qtyWidth, priceWidth);
    }

    private static float ComputeMaxWidth(
        IReadOnlyList<HudLine> lines,
        ImFontPtr font,
        float fontSize,
        ColumnLayout columns,
        float padding)
    {
        var tableWidth = columns.ItemWidth + ColumnGap + columns.QtyWidth + ColumnGap + columns.PriceWidth;
        var maxWidth = tableWidth;

        foreach (var line in lines)
        {
            if (line.Type is HudLineType.CurrencyHeader or HudLineType.CurrencyRow)
            {
                continue;
            }

            if (line.Type == HudLineType.Spacer || string.IsNullOrEmpty(line.Text))
            {
                continue;
            }

            maxWidth = Math.Max(maxWidth, MeasureTextWidth(font, fontSize, line.Text));
        }

        return maxWidth;
    }

    private static float MeasureTextWidth(ImFontPtr font, float fontSize, string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0f;
        }

        return font.CalcTextSizeA(fontSize, float.MaxValue, 0f, text).X;
    }

    private static List<HudLine> BuildLines(
        BetterLootTrackerSettings settings,
        SessionLootState state,
        LootTrackerService tracker)
    {
        var lines = new List<HudLine>(32);

        AppendRecentPickupsSection(lines, settings, state, tracker);
        if (lines.Count > 0)
        {
            AppendSpacer(lines);
        }

        AppendMapSection(lines, "Best Map", state.BestMap.ZoneName, state.BestMap.Loot, state.BestMap.HasCurrency, settings, tracker, colorCodeMapValue: true);
        AppendSpacer(lines);
        AppendCurrentMapSection(lines, state, settings, tracker);
        AppendSpacer(lines);

        if (settings.ShowLastSessionInsteadOfLastMap)
        {
            var lastSession = tracker.GetLastSessionView(true);
            AppendMapSection(
                lines,
                "Last Session",
                lastSession.ZoneName,
                lastSession.Loot,
                lastSession.HasCurrency,
                settings,
                tracker,
                colorCodeMapValue: false);
        }
        else
        {
            AppendMapSection(lines, "Last Map", state.LastMap.ZoneName, state.LastMap.Loot, state.LastMap.HasCurrency, settings, tracker, colorCodeMapValue: true);
        }
        AppendSpacer(lines);
        AppendTotalsSection(lines, "Total Session", state.Session, settings, tracker);

        if (lines.Count == 0)
        {
            lines.Add(new HudLine(
                HudLineType.Plain,
                state.TrackingPaused ? "Tracking paused in town or hideout." : "No loot tracked yet.",
                IsMuted: true));
        }

        return lines;
    }

    private static void AppendRecentPickupsSection(
        List<HudLine> lines,
        BetterLootTrackerSettings settings,
        SessionLootState state,
        LootTrackerService tracker)
    {
        if (!settings.ShowRecentPickupsOnHud || state.RecentPickups.Count == 0)
        {
            return;
        }

        lines.Add(new HudLine(HudLineType.Plain, "Recent loot", IsHeader: true));
        lines.Add(new HudLine(
            HudLineType.CurrencyHeader,
            IsMuted: true,
            Item: "Item",
            Qty: "Qty",
            Price: "Price"));

        foreach (var pickup in state.RecentPickups.Take(settings.HudMaxRecentPickupLines))
        {
            var priceText = FormatPickupPrice(pickup, settings, tracker);
            lines.Add(new HudLine(
                HudLineType.CurrencyRow,
                Item: pickup.DisplayName,
                Qty: pickup.Quantity.ToString(),
                Price: priceText));
        }

        if (settings.ShowDivineEquivalent && tracker.HasPriceData && state.Session.DivineEquivalent > 0)
        {
            var sessionTotal = tracker.GetDisplayedValue(state.Session.DivineEquivalent, settings.ValueUnit);
            var suffix = tracker.GetValueSuffix(settings.ValueUnit);
            lines.Add(new HudLine(
                HudLineType.Plain,
                $"Session total: {sessionTotal:0.###}{suffix}",
                IsHeader: true));
        }
    }

    private static string FormatPickupPrice(
        LootEntry pickup,
        BetterLootTrackerSettings settings,
        LootTrackerService tracker)
    {
        if (!settings.ShowDivineEquivalent || !tracker.HasPriceData)
        {
            return "—";
        }

        if (pickup.DivineValue > 0)
        {
            var displayed = tracker.GetDisplayedValue(pickup.DivineValue, settings.ValueUnit);
            return $"{displayed:0.###}{tracker.GetValueSuffix(settings.ValueUnit)}";
        }

        if (tracker.TryGetDivineUnitValue(pickup.PriceId, pickup.ItemPath, out var unitValue) && unitValue > 0)
        {
            var lineValue = tracker.GetDisplayedValue(unitValue * pickup.Quantity, settings.ValueUnit);
            return $"{lineValue:0.###}{tracker.GetValueSuffix(settings.ValueUnit)}";
        }

        return "—";
    }

    private static void AppendCurrentMapSection(
        List<HudLine> lines,
        SessionLootState state,
        BetterLootTrackerSettings settings,
        LootTrackerService tracker)
    {
        var zoneLabel = string.IsNullOrWhiteSpace(state.ActiveMapZoneName) ? "—" : state.ActiveMapZoneName;
        lines.Add(new HudLine(HudLineType.Plain, $"Current Map: {zoneLabel}", IsHeader: true));

        if (!state.CurrentMap.HasLoot)
        {
            lines.Add(new HudLine(HudLineType.Plain, "No loot tracked yet.", IsMuted: true));
            return;
        }

        AppendValueLine(lines, state.CurrentMap.DivineEquivalent, settings, tracker, colorCodeAsMapValue: true);
        AppendCurrencyTableLines(lines, state.CurrentMap, settings, tracker, settings.HudMaxCurrencyLines);
    }

    private static void AppendMapSection(
        List<HudLine> lines,
        string title,
        string zoneName,
        LootTotals loot,
        bool hasCurrency,
        BetterLootTrackerSettings settings,
        LootTrackerService tracker,
        bool colorCodeMapValue)
    {
        var zoneLabel = string.IsNullOrWhiteSpace(zoneName) ? "—" : zoneName;
        lines.Add(new HudLine(HudLineType.Plain, $"{title}: {zoneLabel}", IsHeader: true));

        if (!hasCurrency)
        {
            lines.Add(new HudLine(HudLineType.Plain, "No loot tracked yet.", IsMuted: true));
            return;
        }

        AppendValueLine(lines, loot.DivineEquivalent, settings, tracker, colorCodeAsMapValue: colorCodeMapValue);
        AppendCurrencyTableLines(lines, loot, settings, tracker, settings.HudMaxCurrencyLines);
    }

    private static void AppendTotalsSection(
        List<HudLine> lines,
        string title,
        LootTotals totals,
        BetterLootTrackerSettings settings,
        LootTrackerService tracker)
    {
        lines.Add(new HudLine(HudLineType.Plain, title, IsHeader: true));

        if (!totals.HasLoot)
        {
            lines.Add(new HudLine(HudLineType.Plain, "No loot tracked yet.", IsMuted: true));
            return;
        }

        AppendValueLine(lines, totals.DivineEquivalent, settings, tracker);
        AppendCurrencyTableLines(lines, totals, settings, tracker, settings.HudMaxCurrencyLines);
    }

    private static void AppendValueLine(
        List<HudLine> lines,
        double divineEquivalent,
        BetterLootTrackerSettings settings,
        LootTrackerService tracker,
        bool colorCodeAsMapValue = false)
    {
        if (!settings.ShowDivineEquivalent || !tracker.HasPriceData)
        {
            return;
        }

        var value = tracker.GetDisplayedValue(divineEquivalent, settings.ValueUnit);
        var suffix = tracker.GetValueSuffix(settings.ValueUnit);
        Vector4? valueColor = null;
        if (colorCodeAsMapValue &&
            MapValueRating.TryGetRatingColor(settings, tracker, divineEquivalent, out var ratingColor))
        {
            valueColor = ratingColor;
        }

        lines.Add(new HudLine(
            HudLineType.Plain,
            $"Value: {value:0.###} {suffix}",
            TextColor: valueColor));
    }

    private static void AppendCurrencyTableLines(
        List<HudLine> lines,
        LootTotals totals,
        BetterLootTrackerSettings settings,
        LootTrackerService tracker,
        int maxLines)
    {
        if (!settings.ShowHudCurrencyLines || !totals.HasLoot || maxLines <= 0)
        {
            return;
        }

        lines.Add(new HudLine(
            HudLineType.CurrencyHeader,
            IsMuted: true,
            Item: "Item",
            Qty: "Qty",
            Price: "Price"));

        foreach (var itemPath in totals.QuantitiesByPath.Keys
                     .OrderByDescending(path => GetLineValue(totals, path, tracker))
                     .ThenByDescending(path => totals.QuantitiesByPath[path])
                     .Take(maxLines))
        {
            var quantity = totals.QuantitiesByPath[itemPath];
            var displayName = tracker.GetItemDisplayName(itemPath, totals);
            var priceId = tracker.ResolvePriceId(itemPath, totals);
            var hasUnitValue = tracker.TryGetDivineUnitValue(priceId, itemPath, out var divineUnitValue);

            string priceText;
            if (settings.ShowDivineEquivalent && hasUnitValue && divineUnitValue > 0)
            {
                var lineValue = tracker.GetDisplayedValue(divineUnitValue * quantity, settings.ValueUnit);
                priceText = $"{lineValue:0.###}{tracker.GetValueSuffix(settings.ValueUnit)}";
            }
            else
            {
                priceText = "—";
            }

            lines.Add(new HudLine(
                HudLineType.CurrencyRow,
                Item: displayName,
                Qty: quantity.ToString(),
                Price: priceText));
        }
    }

    private static void AppendSpacer(List<HudLine> lines)
    {
        if (lines.Count > 0)
        {
            lines.Add(new HudLine(HudLineType.Spacer));
        }
    }

    private static double GetLineValue(LootTotals totals, string itemPath, LootTrackerService tracker)
    {
        if (!tracker.TryGetDivineUnitValueForItem(itemPath, totals, out var unitValue))
        {
            return 0;
        }

        return unitValue * totals.QuantitiesByPath[itemPath];
    }
}
