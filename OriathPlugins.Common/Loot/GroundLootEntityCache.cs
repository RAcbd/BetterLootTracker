namespace OriathPlugins.Common.Loot;

using OriathHub.RemoteObjects.States.InGameStateObjects;

/// <summary>
/// Tracks ground-loot entities using SDK 0.10.1 snapshot + per-frame deltas.
/// Seed on area change or mid-area enable via <see cref="AreaInstance.GetAwakeEntitiesSnapshot" />,
/// then follow <see cref="AreaInstance.EntitiesAddedThisFrame" /> /
/// <see cref="AreaInstance.EntitiesRemovedThisFrame" />.
/// </summary>
public sealed class GroundLootEntityCache
{
    private readonly Dictionary<uint, Entity> entities = new();
    private string seededAreaId = string.Empty;

    public int Count => entities.Count;

    public IEnumerable<Entity> Entities => entities.Values;

    public void Reset()
    {
        entities.Clear();
        seededAreaId = string.Empty;
    }

    public void UpdateFrame(AreaInstance area, string areaId)
    {
        if (string.IsNullOrEmpty(areaId))
        {
            return;
        }

        if (!seededAreaId.Equals(areaId, StringComparison.Ordinal))
        {
            SeedArea(area, areaId);
            return;
        }

        foreach (var entity in area.EntitiesAddedThisFrame)
        {
            TryAdd(entity);
        }

        foreach (var entity in area.EntitiesRemovedThisFrame)
        {
            entities.Remove(entity.Id);
        }

        PruneStale();
    }

    public bool TryGetEntity(uint entityId, out Entity entity) => entities.TryGetValue(entityId, out entity!);

    private void SeedArea(AreaInstance area, string areaId)
    {
        entities.Clear();
        seededAreaId = areaId;

        foreach (var entity in area.GetAwakeEntitiesSnapshot())
        {
            TryAdd(entity);
        }
    }

    private void TryAdd(Entity entity)
    {
        if (!entity.IsValid || !GroundLootRules.IsGroundLootEntity(entity) || LootPathMatcher.IsGoldEntity(entity))
        {
            return;
        }

        entities[entity.Id] = entity;
    }

    private void PruneStale()
    {
        foreach (var id in entities.Keys.ToList())
        {
            if (!entities.TryGetValue(id, out var entity) ||
                !entity.IsValid ||
                !GroundLootRules.IsGroundLootEntity(entity) ||
                LootPathMatcher.IsGoldEntity(entity))
            {
                entities.Remove(id);
            }
        }
    }
}
