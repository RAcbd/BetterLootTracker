namespace OriathPlugins.Common.Loot;

using System.Numerics;
using OriathHub.RemoteEnums;
using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.States;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Pricing;

public static class GroundLootScanner
{
    public static IEnumerable<Entity> EnumerateAwakeGroundLoot(AreaInstance area)
    {
        foreach (var (_, entity) in area.AwakeEntities)
        {
            if (entity.IsValid && GroundLootRules.IsGroundLootEntity(entity))
            {
                yield return entity;
            }
        }
    }

    public static IReadOnlyList<GroundLootCandidate> Scan(
        InGameState inGame,
        AreaInstance area,
        IEnumerable<Entity> groundLootEntities,
        GroundLootScanSettings settings,
        LootPriceService? prices,
        Func<uint, int, int, bool>? isIgnored,
        GroundLootScanDiagnostics diagnostics,
        IList<GroundLootMarker>? markers)
    {
        diagnostics.AwakeEntities = area.AwakeEntities.Count;
        diagnostics.GroundEntities = 0;
        diagnostics.FilteredByPath = 0;
        diagnostics.OutOfRange = 0;
        diagnostics.MissingScreen = 0;
        diagnostics.Clickable = 0;
        markers?.Clear();

        var results = new List<GroundLootCandidate>();
        if (!area.Player.IsValid)
        {
            return results;
        }

        foreach (var entity in groundLootEntities)
        {
            if (!entity.IsValid || !GroundLootRules.IsGroundLootEntity(entity))
            {
                continue;
            }

            diagnostics.GroundEntities++;

            if (LootPathMatcher.IsGoldEntity(entity))
            {
                diagnostics.FilteredByPath++;
                continue;
            }

            var itemPath = GroundLootRules.ResolveItemPath(entity) ?? string.Empty;
            var displayName = GroundLootRules.ResolveDisplayName(entity);

            var hasScreen = ScreenPositionResolver.TryGetBaseClientPosition(inGame, entity, out var clientPosition) &&
                            ScreenPositionResolver.IsWithinClientArea(clientPosition);
            if (hasScreen &&
                isIgnored?.Invoke(entity.Id, (int)clientPosition.X, (int)clientPosition.Y) == true)
            {
                diagnostics.FilteredByPath++;
                continue;
            }

            var inRange = IsInPickupRange(area, entity, settings.PickupDistance, out var distance);
            if (settings.CurrencyOnly &&
                !(settings.AlwaysPickupWaystonesAndTablets && LootPathMatcher.IsAlwaysPickupPath(itemPath)) &&
                !LootPathMatcher.IsCurrencyPickup(entity, itemPath, settings.AlwaysPickupWaystonesAndTablets))
            {
                diagnostics.FilteredByPath++;
                if (markers is not null && hasScreen)
                {
                    markers.Add(new GroundLootMarker(clientPosition, GroundLootMarkerKind.Filtered, displayName, itemPath, 0));
                }

                continue;
            }

            var divineValue = 0d;
            if (prices is not null && inRange)
            {
                prices.TryEvaluateGroundLoot(entity, out divineValue, out _);
            }

            var priority = prices?.GetPickupPriority(
                itemPath,
                divineValue,
                settings.AlwaysPickupWaystonesAndTablets) ?? 10;

            var passesFilter = LootPathMatcher.ShouldPickup(
                entity,
                itemPath,
                divineValue,
                settings,
                prices);

            if (markers is not null && hasScreen)
            {
                var kind = GroundLootMarkerKind.NoScreen;
                if (!passesFilter)
                {
                    kind = GroundLootMarkerKind.Filtered;
                }
                else if (!inRange)
                {
                    kind = GroundLootMarkerKind.OutOfRange;
                }
                else if (isIgnored?.Invoke(entity.Id, (int)clientPosition.X, (int)clientPosition.Y) == true)
                {
                    kind = GroundLootMarkerKind.Filtered;
                }
                else if (divineValue >= settings.MinDivineValue && settings.UseValueFilter)
                {
                    kind = GroundLootMarkerKind.Valuable;
                }
                else
                {
                    kind = GroundLootMarkerKind.Clickable;
                }

                markers.Add(new GroundLootMarker(clientPosition, kind, displayName, itemPath, divineValue));
            }

            if (!passesFilter)
            {
                diagnostics.FilteredByPath++;
                continue;
            }

            if (!inRange)
            {
                diagnostics.OutOfRange++;
                continue;
            }

            if (!hasScreen)
            {
                diagnostics.MissingScreen++;
            }
            else
            {
                diagnostics.Clickable++;
            }

            results.Add(new GroundLootCandidate(
                entity.Id,
                itemPath,
                displayName,
                distance,
                clientPosition,
                hasScreen,
                divineValue,
                priority));
        }

        results.Sort(static (left, right) =>
        {
            var distanceCompare = left.Distance.CompareTo(right.Distance);
            if (distanceCompare != 0)
            {
                return distanceCompare;
            }

            var priorityCompare = left.PickupPriority.CompareTo(right.PickupPriority);
            if (priorityCompare != 0)
            {
                return priorityCompare;
            }

            var valueCompare = right.DivineValue.CompareTo(left.DivineValue);
            if (valueCompare != 0)
            {
                return valueCompare;
            }

            var leftRank = left.HasScreenPosition ? 0 : 1;
            var rightRank = right.HasScreenPosition ? 0 : 1;
            return leftRank.CompareTo(rightRank);
        });

        return results;
    }

    public static bool TryFindPickupTarget(
        InGameState inGame,
        AreaInstance area,
        IEnumerable<Entity> groundLootEntities,
        GroundLootScanSettings settings,
        LootPriceService? prices,
        Func<uint, int, int, bool>? isIgnored,
        GroundLootScanDiagnostics diagnostics,
        out GroundLootCandidate target,
        uint? stickyEntityId = null)
    {
        target = default;
        diagnostics.AwakeEntities = area.AwakeEntities.Count;
        diagnostics.GroundEntities = 0;
        diagnostics.FilteredByPath = 0;
        diagnostics.OutOfRange = 0;
        diagnostics.MissingScreen = 0;
        diagnostics.Clickable = 0;

        if (!area.Player.IsValid)
        {
            return false;
        }

        GroundLootCandidate? best = null;
        GroundLootCandidate? sticky = null;
        foreach (var entity in groundLootEntities)
        {
            if (!entity.IsValid || !GroundLootRules.IsGroundLootEntity(entity))
            {
                continue;
            }

            diagnostics.GroundEntities++;

            if (LootPathMatcher.IsGoldEntity(entity))
            {
                diagnostics.FilteredByPath++;
                continue;
            }

            if (!TryBuildPickupCandidate(
                    inGame,
                    area,
                    entity,
                    settings,
                    prices,
                    isIgnored,
                    diagnostics,
                    out var candidate))
            {
                continue;
            }

            if (stickyEntityId.HasValue && entity.Id == stickyEntityId.Value)
            {
                sticky = candidate;
            }

            if (IsBetterPickupCandidate(candidate, best))
            {
                best = candidate;
            }
        }

        if (sticky.HasValue)
        {
            target = sticky.Value;
            return true;
        }

        if (best is null)
        {
            return false;
        }

        target = best.Value;
        return true;
    }

    private static bool TryBuildPickupCandidate(
        InGameState inGame,
        AreaInstance area,
        Entity entity,
        GroundLootScanSettings settings,
        LootPriceService? prices,
        Func<uint, int, int, bool>? isIgnored,
        GroundLootScanDiagnostics diagnostics,
        out GroundLootCandidate candidate)
    {
        candidate = default;
        if (LootPathMatcher.IsGoldEntity(entity))
        {
            diagnostics.FilteredByPath++;
            return false;
        }

        if (!IsInPickupRange(area, entity, settings.PickupDistance, out var distance))
        {
            diagnostics.OutOfRange++;
            return false;
        }

        if (!ScreenPositionResolver.TryGetBaseClientPosition(inGame, entity, out var clientPosition) ||
            !ScreenPositionResolver.IsWithinClientArea(clientPosition))
        {
            diagnostics.MissingScreen++;
            return false;
        }

        if (!ScreenPositionResolver.TryGetClickableScreenPosition(clientPosition, out _))
        {
            diagnostics.MissingScreen++;
            return false;
        }

        if (isIgnored?.Invoke(entity.Id, (int)clientPosition.X, (int)clientPosition.Y) == true)
        {
            diagnostics.FilteredByPath++;
            return false;
        }

        string? itemPath = null;
        if (settings.CurrencyOnly || settings.UseValueFilter)
        {
            itemPath = GroundLootRules.ResolveItemPath(entity) ?? string.Empty;
            if (!PassesPickupFilter(entity, itemPath, settings, divineValue: 0d, prices))
            {
                diagnostics.FilteredByPath++;
                return false;
            }
        }

        itemPath ??= entity.Path ?? string.Empty;
        diagnostics.Clickable++;
        var needsPriority = settings.UseValueFilter || settings.CurrencyOnly;
        var divineValue = 0d;
        if (prices is not null)
        {
            prices.TryEvaluateGroundLoot(entity, out divineValue, out _);
            if (settings.UseValueFilter && divineValue < settings.MinDivineValue &&
                !(settings.AlwaysPickupWaystonesAndTablets && LootPathMatcher.IsAlwaysPickupPath(itemPath)))
            {
                diagnostics.FilteredByPath++;
                return false;
            }
        }

        var priority = needsPriority
            ? prices?.GetPickupPriority(
                  itemPath,
                  divineValue,
                  settings.AlwaysPickupWaystonesAndTablets) ??
              GetPathOnlyPickupPriority(itemPath, settings.AlwaysPickupWaystonesAndTablets)
            : 0;

        candidate = new GroundLootCandidate(
            entity.Id,
            itemPath,
            string.Empty,
            distance,
            clientPosition,
            true,
            divineValue,
            priority);
        return true;
    }

    private static bool PassesPickupFilter(
        Entity entity,
        string itemPath,
        GroundLootScanSettings settings,
        double divineValue,
        LootPriceService? prices) =>
        LootPathMatcher.ShouldPickup(entity, itemPath, divineValue, settings, prices);

    private static bool IsBetterPickupCandidate(GroundLootCandidate candidate, GroundLootCandidate? best)
    {
        if (best is null)
        {
            return true;
        }

        var current = best.Value;
        if (candidate.Distance != current.Distance)
        {
            return candidate.Distance < current.Distance;
        }

        if (candidate.PickupPriority != current.PickupPriority)
        {
            return candidate.PickupPriority < current.PickupPriority;
        }

        return candidate.DivineValue > current.DivineValue;
    }

    private static int GetPathOnlyPickupPriority(string? itemPath, bool alwaysPickupWaystonesAndTablets)
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

        return 10;
    }

    private static bool IsInPickupRange(
        AreaInstance area,
        Entity entity,
        float pickupDistance,
        out float distance)
    {
        distance = 0f;
        if (!entity.IsValid)
        {
            return false;
        }

        if (entity.Zones is NearbyZones.InnerCircle or NearbyZones.OuterCircle)
        {
            distance = area.Player.IsValid ? entity.DistanceFrom(area.Player) : 1f;
            return distance > 0f && distance <= pickupDistance;
        }

        var player = area.Player;
        if (!player.IsValid)
        {
            return false;
        }

        distance = entity.DistanceFrom(player);
        return distance > 0f && distance <= pickupDistance;
    }
}
