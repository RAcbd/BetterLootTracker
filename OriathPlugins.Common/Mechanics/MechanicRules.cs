namespace OriathPlugins.Common.Mechanics;

using OriathHub.RemoteEnums;
using OriathHub.RemoteEnums.Entity;
using OriathHub.RemoteObjects.Components;
using OriathHub.RemoteObjects.States.InGameStateObjects;

internal static class MechanicRules
{
    public static bool TryClassify(
        Entity entity,
        MechanicScanSettings settings,
        out MechanicKind kind,
        out string displayName)
    {
        kind = default;
        displayName = string.Empty;
        if (!entity.IsValid || entity.EntityType is EntityTypes.Player)
        {
            return false;
        }

        var path = entity.Path ?? string.Empty;
        if (settings.ShowEssences && TryClassifyEssence(entity, path, out displayName))
        {
            kind = MechanicKind.Essence;
            return true;
        }

        if (IsLivingCombatActor(entity, path))
        {
            return false;
        }

        if (settings.ShowRituals && path.Contains("RitualRuneInteractable", StringComparison.OrdinalIgnoreCase))
        {
            kind = MechanicKind.Ritual;
            displayName = "Ritual altar";
            return true;
        }

        if (settings.ShowStrongboxes && TryClassifyStrongbox(entity, out displayName))
        {
            kind = MechanicKind.Strongbox;
            return true;
        }

        if (settings.ShowShrines && entity.TryGetComponent<Shrine>(out var shrine) && !shrine.IsUsed)
        {
            kind = MechanicKind.Shrine;
            displayName = "Shrine";
            return true;
        }

        return false;
    }

    private static bool TryClassifyStrongbox(Entity entity, out string displayName)
    {
        displayName = "Strongbox";
        if (!entity.TryGetComponent<Chest>(out var chest) || chest.IsOpened)
        {
            return false;
        }

        return chest.IsStrongbox || entity.EntitySubtype is EntitySubtypes.Strongbox;
    }

    private static bool TryClassifyEssence(Entity entity, string path, out string displayName)
    {
        displayName = "Essence";
        if (!IsImprisonedEssence(entity, path))
        {
            return false;
        }

        displayName = IsEssenceMonolithPath(path) ? "Essence monolith" : "Essence monster";
        return true;
    }

    private static bool IsImprisonedEssence(Entity entity, string path)
    {
        if (!entity.IsValid)
        {
            return false;
        }

        if (!entity.TryGetComponent<Life>(out var life) || !life.IsAlive)
        {
            return false;
        }

        if (IsEssenceMonsterPath(path) || IsEssenceMonolithPath(path))
        {
            return true;
        }

        if (entity.TryGetComponent<MinimapIcon>(out var minimapIcon) &&
            !string.IsNullOrWhiteSpace(minimapIcon.IconName) &&
            minimapIcon.IconName.Contains("Essence", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entity.TryGetComponent<Animated>(out var animated) &&
            !string.IsNullOrWhiteSpace(animated.Path) &&
            animated.Path.Contains("essence", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!IsEssenceMonsterPath(path))
        {
            return false;
        }

        if (entity.TryGetComponent<ObjectMagicProperties>(out var magicProperties))
        {
            foreach (var modName in magicProperties.ModNames)
            {
                if (!string.IsNullOrWhiteSpace(modName) &&
                    modName.Contains("Essence", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsEssenceMonsterPath(string path) =>
        path.Contains("Metadata/Monsters/Essence", StringComparison.OrdinalIgnoreCase) ||
        path.Contains("/EssenceMonster", StringComparison.OrdinalIgnoreCase);

    private static bool IsEssenceMonolithPath(string path) =>
        path.Contains("EssenceMonolith", StringComparison.OrdinalIgnoreCase) ||
        (path.Contains("Essence", StringComparison.OrdinalIgnoreCase) &&
         (path.Contains("Metadata/MiscellaneousObjects/", StringComparison.OrdinalIgnoreCase) ||
          path.Contains("Metadata/Terrain/", StringComparison.OrdinalIgnoreCase)));

    private static bool IsLivingCombatActor(Entity entity, string path)
    {
        if (entity.EntityType is EntityTypes.Monster)
        {
            return !IsImprisonedEssence(entity, path);
        }

        return false;
    }
}
