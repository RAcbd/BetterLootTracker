namespace OriathPlugins.Common.Mechanics;

using OriathHub.RemoteObjects.States;
using OriathHub.RemoteObjects.States.InGameStateObjects;
using OriathPlugins.Common.Loot;

public static class MechanicScanner
{
    public static IReadOnlyList<MechanicMarker> Scan(
        InGameState inGame,
        AreaInstance area,
        MechanicScanSettings settings)
    {
        var results = new List<MechanicMarker>();
        if (!area.Player.IsValid)
        {
            return results;
        }

        foreach (var (_, entity) in area.AwakeEntities)
        {
            if (!MechanicRules.TryClassify(entity, settings, out var kind, out var displayName))
            {
                continue;
            }

            var distance = entity.DistanceFrom(area.Player);
            if (distance <= 0f || distance > settings.MaxDistance)
            {
                continue;
            }

            if (!ScreenPositionResolver.TryGetBaseClientPosition(inGame, entity, out var clientPosition) ||
                !ScreenPositionResolver.TryGetBaseWorldPosition(entity, out var worldCenter, out var baseHeight))
            {
                continue;
            }

            results.Add(new MechanicMarker(clientPosition, worldCenter, baseHeight, kind, displayName, distance));
        }

        results.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
        return results;
    }
}
