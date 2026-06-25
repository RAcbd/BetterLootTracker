namespace BetterLootTracker;

using System.Collections;
using System.Numerics;
using Coroutine;
using ImGuiNET;
using OriathHub;
using OriathHub.CoroutineEvents;
using OriathHub.Plugin;
using OriathHub.RemoteEnums;
using OriathHub.Utils;
using OriathPlugins.Common.Pricing;

public sealed class BetterLootTrackerPlugin : PluginBase
{
    private readonly SessionLootState state = new();
    private readonly LootTrackerService tracker = new();
    private BetterLootTrackerSettings settings = new();

    private ActiveCoroutine? updateCoroutine;
    private FileInfo settingsFile = null!;
    private string selectedSessionId = string.Empty;

    public override string Name => "BetterLootTracker";

    public override string Description => "Tracks loot per map and session with host pricing and HUD overlay.";

    public override void OnEnable(bool isGameOpened)
    {
        settingsFile = new FileInfo(Path.Combine(DllDirectory, "config", "settings.json"));
        settings = JsonHelper.CreateOrLoadJsonFile<BetterLootTrackerSettings>(settingsFile);
        tracker.InitializeData(DllDirectory);
        selectedSessionId = tracker.SessionHistory.LatestSession?.Id ?? string.Empty;
        ReloadPrices();
        state.ResetSession();

        updateCoroutine = CoroutineHandler.Start(OnPerFrameUpdate(), $"{Name}.Update");
        CoroutineHandler.Start(OnAreaChange(), $"{Name}.AreaChange");
    }

    public override void OnDisable()
    {
        updateCoroutine?.Cancel();
        updateCoroutine = null;
    }

    public override void DrawDashboard()
    {
        DrawAllSections();

        ImGui.Spacing();
        DrawSavedSessionsPanel();
        ImGui.Spacing();

        if (ImGui.Button("Reset session"))
        {
            if (state.Session.HasLoot)
            {
                tracker.TrySaveSession(state);
                selectedSessionId = tracker.SessionHistory.LatestSession?.Id ?? selectedSessionId;
            }

            tracker.ResetSession(state);
        }

        ImGui.SameLine();
        if (ImGui.Button("Reload prices"))
        {
            ReloadPrices();
        }

        ImGui.SameLine();
        if (ImGui.Button("Reload currency names"))
        {
            tracker.ReloadCurrencyNames(DllDirectory);
        }

        ImGui.TextDisabled(tracker.PriceStatusMessage);
        ImGui.TextDisabled($"Pricing league: {tracker.ActiveLeague}");
        ImGui.TextDisabled($"Currency names: {tracker.CurrencyNamesFilePath}");
        ImGui.TextDisabled("Prices come from OriathHub Core.Prices (poe.ninja).");
        ImGui.TextDisabled("Manual override: byPathNinjaId in currency-names.json.");
        if (state.TrackingPaused)
        {
            ImGui.TextColored(new Vector4(0.9f, 0.7f, 0.2f, 1f), "Tracking paused in town.");
        }
    }

    public override void DrawSettings()
    {
        ImGui.Checkbox("Show HUD overlay", ref settings.ShowOverlay);
        if (settings.ShowOverlay)
        {
            ImGui.Indent();
            ImGui.Checkbox("Draw only in hideout", ref settings.DrawOnlyInHideout);
            ImGui.Unindent();
        }
        ImGui.Checkbox("Pause tracking in towns", ref settings.PauseInTown);
        ImGui.Checkbox("Show divine equivalent", ref settings.ShowDivineEquivalent);
        ImGui.Checkbox("Show currency lines on HUD", ref settings.ShowHudCurrencyLines);
        ImGui.Checkbox("Show recent pickups on HUD", ref settings.ShowRecentPickupsOnHud);
        ImGui.Checkbox("Show last session instead of last map", ref settings.ShowLastSessionInsteadOfLastMap);
        ImGui.Checkbox("Debug logging", ref settings.EnableDebugLogging);

        ImGui.DragFloat2("HUD position", ref settings.DrawPosition, 1f);
        ImGui.SliderFloat("HUD font size", ref settings.HudFontSize, 10f, 32f);
        ImGui.ColorEdit4("HUD background", ref settings.DefaultBackgroundColor);
        ImGui.ColorEdit4("HUD text color", ref settings.DefaultFontColor);
        ImGui.SliderInt("HUD currency lines", ref settings.HudMaxCurrencyLines, 0, 12);
        ImGui.SliderInt("HUD recent pickup lines", ref settings.HudMaxRecentPickupLines, 0, 20);
        ImGui.DragFloat("Pickup distance", ref settings.PickupDistance, 1f, 50f, 300f);

        ImGui.SliderInt("Recent pickups shown", ref settings.MaxRecentEntries, 5, 50);

        var unitIndex = settings.ValueUnit.ToLowerInvariant() switch
        {
            "chaos" => 1,
            "exalted" => 2,
            _ => 0,
        };
        if (ImGui.Combo("Value unit", ref unitIndex, "Divine\0Chaos\0Exalted\0", 3))
        {
            settings.ValueUnit = unitIndex switch
            {
                1 => "chaos",
                2 => "exalted",
                _ => "divine",
            };
        }

        ImGui.Separator();
        ImGui.Text("Map value rating");
        ImGui.Checkbox("Color-code map values on HUD", ref settings.ColorCodeMapValue);
        var thresholdSuffix = tracker.GetValueSuffix(settings.ValueUnit);
        ImGui.DragFloat(
            $"Good map threshold ({thresholdSuffix})",
            ref settings.MapValueGoodThreshold,
            1f,
            0f,
            1_000_000f);
        ImGui.TextDisabled("Current / Best / Last map values turn green at or above this, red below.");
        if (settings.ColorCodeMapValue)
        {
            ImGui.ColorEdit4("Good value color", ref settings.GoodMapValueColor);
            ImGui.ColorEdit4("Bad value color", ref settings.BadMapValueColor);
        }

        ImGui.Separator();
        DrawCurrencyFilterSettings();

        ImGui.Separator();
        ImGui.ColorEdit4("Dashboard accent", ref settings.AccentColor);
    }

    public override void DrawUI()
    {
        if (!settings.ShowOverlay || !FocusHelper.IsGameOrOverlayForeground())
        {
            return;
        }

        if (!LootHudVisibility.CanDrawOverlay(settings))
        {
            return;
        }

        LootHudRenderer.Draw(settings, state, tracker);
    }

    public override void SaveSettings()
    {
        JsonHelper.SaveToFile(settings, settingsFile);
    }

    private void DrawAllSections()
    {
        DrawMapSection("Best Map", state.BestMap);
        ImGui.Spacing();
        DrawCurrentMapSection();
        ImGui.Spacing();
        if (settings.ShowLastSessionInsteadOfLastMap)
        {
            DrawMapSection("Last Session", tracker.GetLastSessionView(true));
        }
        else
        {
            DrawMapSection("Last Map", state.LastMap);
        }

        ImGui.Spacing();
        DrawTotalsSection("Total Session", state.Session);
    }

    private void DrawSavedSessionsPanel()
    {
        ImGui.TextColored(settings.AccentColor, "Saved Sessions");
        var summaries = tracker.SessionHistory.Summaries;
        if (summaries.Count == 0)
        {
            ImGui.TextDisabled("Sessions are saved when you reset. Files live in data/sessions/.");
            return;
        }

        var selected = summaries.FirstOrDefault(summary => summary.Id == selectedSessionId);
        var preview = string.IsNullOrEmpty(selected.Id)
            ? "Select a saved session..."
            : $"{selected.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} — {selected.LastZoneName ?? "Session"}";

        if (ImGui.BeginCombo("Saved session", preview))
        {
            foreach (var summary in summaries)
            {
                var label =
                    $"{summary.SavedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm} — {summary.LastZoneName ?? "Session"} ({summary.DivineEquivalent:0.###}d)";
                if (ImGui.Selectable(label, summary.Id == selectedSessionId))
                {
                    selectedSessionId = summary.Id;
                    tracker.SessionHistory.TrySelectSession(summary.Id);
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.Button("Use latest saved session"))
        {
            tracker.SessionHistory.UseLatestSession();
            selectedSessionId = tracker.SessionHistory.LatestSession?.Id ?? string.Empty;
        }
    }

    private void DrawCurrentMapSection()
    {
        var zoneLabel = string.IsNullOrWhiteSpace(state.ActiveMapZoneName) ? "—" : state.ActiveMapZoneName;
        ImGui.TextColored(settings.AccentColor, $"Current Map: {zoneLabel}");

        if (settings.ShowDivineEquivalent && tracker.HasPriceData && state.CurrentMap.HasLoot)
        {
            DrawRatedMapValue(state.CurrentMap.DivineEquivalent);
        }

        if (!state.CurrentMap.HasLoot)
        {
            ImGui.TextDisabled("No currency tracked yet.");
            return;
        }

        DrawCurrencyTable("CurrentMapTable", state.CurrentMap);
    }

    private void DrawMapSection(string title, MapLootSnapshot snapshot)
    {
        var zoneLabel = snapshot.HasZone ? snapshot.ZoneName : "—";
        ImGui.TextColored(settings.AccentColor, $"{title}: {zoneLabel}");

        if (!snapshot.HasCurrency)
        {
            ImGui.TextDisabled("No currency tracked yet.");
            return;
        }

        if (settings.ShowDivineEquivalent && tracker.HasPriceData)
        {
            DrawRatedMapValue(snapshot.Loot.DivineEquivalent);
        }

        DrawCurrencyTable($"{title}Table", snapshot.Loot);
    }

    private void DrawTotalsSection(string title, LootTotals totals)
    {
        ImGui.TextColored(settings.AccentColor, title);

        if (settings.ShowDivineEquivalent && tracker.HasPriceData && totals.HasLoot)
        {
            var value = tracker.GetDisplayedValue(totals.DivineEquivalent, settings.ValueUnit);
            ImGui.Text($"Value: {value:0.###} {tracker.GetValueSuffix(settings.ValueUnit)}");
        }

        if (!totals.HasLoot)
        {
            ImGui.TextDisabled("No currency tracked yet.");
            return;
        }

        DrawCurrencyTable($"{title}Table", totals);
    }

    private void DrawRatedMapValue(double divineEquivalent)
    {
        var value = tracker.GetDisplayedValue(divineEquivalent, settings.ValueUnit);
        var suffix = tracker.GetValueSuffix(settings.ValueUnit);
        var label = $"Value: {value:0.###} {suffix}";

        if (MapValueRating.TryGetRatingColor(settings, tracker, divineEquivalent, out var color))
        {
            ImGui.TextColored(color, label);
            return;
        }

        ImGui.Text(label);
    }

    private void DrawCurrencyTable(string tableId, LootTotals totals)
    {
        if (ImGui.BeginTable(tableId, 3, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg | ImGuiTableFlags.SizingStretchProp))
        {
            ImGui.TableSetupColumn("Currency");
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthFixed, 72);
            ImGui.TableHeadersRow();

            foreach (var itemPath in totals.QuantitiesByPath.Keys
                         .OrderByDescending(path => GetLineValue(totals, path))
                         .ThenByDescending(path => totals.QuantitiesByPath[path]))
            {
                var quantity = totals.QuantitiesByPath[itemPath];
                var displayName = tracker.GetItemDisplayName(itemPath, totals);
                var priceId = tracker.ResolvePriceId(itemPath, totals);
                var hasUnitValue = tracker.TryGetDivineUnitValue(priceId, itemPath, out var divineUnitValue);

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(displayName);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted(quantity.ToString());
                ImGui.TableNextColumn();

                if (settings.ShowDivineEquivalent && hasUnitValue && divineUnitValue > 0)
                {
                    var lineValue = tracker.GetDisplayedValue(divineUnitValue * quantity, settings.ValueUnit);
                    ImGui.TextUnformatted($"{lineValue:0.###}{tracker.GetValueSuffix(settings.ValueUnit)}");
                }
                else
                {
                    ImGui.TextDisabled("-");
                }
            }

            ImGui.EndTable();
        }
    }

    private void DrawCurrencyFilterSettings()
    {
        ImGui.Text("Loot to track");
        ImGui.Checkbox("Track all currencies", ref settings.TrackAllCurrencies);
        ImGui.Checkbox("Track gear and uniques", ref settings.TrackAllLoot);

        if (settings.TrackAllCurrencies)
        {
            return;
        }

        if (!tracker.HasPriceData)
        {
            ImGui.TextDisabled("Load host prices to configure the currency list.");
            return;
        }

        if (ImGui.Button("Select all"))
        {
            settings.TrackedCurrencyIds = tracker.GetCurrencyOptions().Select(static o => o.Id).ToList();
        }

        ImGui.SameLine();
        if (ImGui.Button("Clear all"))
        {
            settings.TrackedCurrencyIds.Clear();
        }

        ImGui.InputText("Search currencies", ref settings.CurrencyFilterSearch, 128);

        var search = settings.CurrencyFilterSearch.Trim();
        var options = tracker.GetCurrencyOptions()
            .Where(option => string.IsNullOrEmpty(search) ||
                             option.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                             option.Id.Contains(search, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (ImGui.BeginCombo("Quick add currency", "Choose currency..."))
        {
            foreach (var option in options.Take(40))
            {
                var isTracked = settings.TrackedCurrencyIds.Contains(option.Id, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Selectable($"{option.Name}##combo_{option.Id}", isTracked))
                {
                    if (!isTracked)
                    {
                        settings.TrackedCurrencyIds.Add(option.Id);
                    }
                }
            }

            ImGui.EndCombo();
        }

        if (ImGui.BeginChild("CurrencyFilterList", new Vector2(0, 220)))
        {
            foreach (var option in options)
            {
                var isTracked = settings.TrackedCurrencyIds.Contains(option.Id, StringComparer.OrdinalIgnoreCase);
                if (ImGui.Checkbox($"{option.Name}##filter_{option.Id}", ref isTracked))
                {
                    if (isTracked)
                    {
                        if (!settings.TrackedCurrencyIds.Contains(option.Id, StringComparer.OrdinalIgnoreCase))
                        {
                            settings.TrackedCurrencyIds.Add(option.Id);
                        }
                    }
                    else
                    {
                        settings.TrackedCurrencyIds.RemoveAll(id =>
                            id.Equals(option.Id, StringComparison.OrdinalIgnoreCase));
                    }
                }
            }

            ImGui.EndChild();
        }
        ImGui.TextDisabled($"{settings.TrackedCurrencyIds.Count} currencies selected");
    }

    private double GetLineValue(LootTotals totals, string itemPath)
    {
        if (!tracker.TryGetDivineUnitValueForItem(itemPath, totals, out var unitValue))
        {
            return 0;
        }

        return unitValue * totals.QuantitiesByPath[itemPath];
    }

    private void ReloadPrices()
    {
        tracker.RefreshPrices();
        tracker.RefreshStoredPrices(state);
    }

    private IEnumerator<Wait> OnAreaChange()
    {
        while (true)
        {
            yield return new Wait(RemoteEvents.AreaChanged);
            tracker.OnAreaChanged(state);
        }
    }

    private IEnumerator<Wait> OnPerFrameUpdate()
    {
        while (true)
        {
            yield return new Wait(OriathEvents.PerFrameDataUpdate);
            UpdateTracking();
        }
    }

    private void UpdateTracking()
    {
        if (Core.States.GameCurrentState != GameStateTypes.InGameState)
        {
            return;
        }

        var inGame = Core.States.InGameStateObject;
        var area = inGame.CurrentAreaInstance;
        var areaDetails = inGame.CurrentWorldInstance.AreaDetails;
        var isTown = areaDetails.IsTown || areaDetails.IsHideout;
        var trackingAllowed = !(settings.PauseInTown && isTown);

        tracker.ProcessFrame(
            area,
            state,
            settings,
            areaDetails.Name,
            areaDetails.Id,
            isTown,
            trackingAllowed);
    }
}
