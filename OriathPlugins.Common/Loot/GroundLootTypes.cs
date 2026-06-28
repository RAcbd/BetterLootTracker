namespace OriathPlugins.Common.Loot;

using System.Numerics;

public enum GroundLootMarkerKind
{
    Clickable,
    Filtered,
    OutOfRange,
    NoScreen,
    Valuable,
}

public readonly record struct GroundLootMarker(
    Vector2 ClientPosition,
    GroundLootMarkerKind Kind,
    string Label,
    string ItemPath,
    double DivineValue);

public readonly record struct GroundLootCandidate(
    uint EntityId,
    string ItemPath,
    string DisplayName,
    float Distance,
    Vector2 ClientPosition,
    bool HasScreenPosition,
    double DivineValue,
    int PickupPriority);

public sealed class GroundLootScanSettings
{
    public bool StackablesOnly
    {
        get => CurrencyOnly;
        set => CurrencyOnly = value;
    }

    public bool CurrencyOnly = true;

    public bool UseValueFilter;

    public double MinDivineValue;

    public float PickupDistance = 600f;

    public bool AlwaysPickupWaystonesAndTablets = true;
}

public sealed class GroundLootScanDiagnostics
{
    public int AwakeEntities;

    public int GroundEntities;

    public int FilteredByPath;

    public int OutOfRange;

    public int MissingScreen;

    public int Clickable;
}
