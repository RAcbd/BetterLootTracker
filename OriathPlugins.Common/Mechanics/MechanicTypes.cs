namespace OriathPlugins.Common.Mechanics;

public enum MechanicKind
{
    Essence,
    Ritual,
    Strongbox,
    Shrine,
}

public readonly record struct MechanicMarker(
    System.Numerics.Vector2 ClientPosition,
    System.Numerics.Vector2 WorldCenter,
    float BaseHeight,
    MechanicKind Kind,
    string Label,
    float Distance);

public sealed class MechanicScanSettings
{
    public bool ShowEssences = true;

    public bool ShowRituals = true;

    public bool ShowStrongboxes = true;

    public bool ShowShrines = true;

    public float MaxDistance = 900f;
}
