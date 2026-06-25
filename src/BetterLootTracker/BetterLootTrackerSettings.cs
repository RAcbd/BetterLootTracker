namespace BetterLootTracker;

using System.Numerics;

public sealed class BetterLootTrackerSettings
{
    public bool ShowOverlay = true;

    public bool DrawOnlyInHideout;
    public bool PauseInTown = true;
    public bool ShowDivineEquivalent = true;
    public bool TrackAllCurrencies = true;
    public bool TrackAllLoot = true;
    public bool ShowHudCurrencyLines = true;
    public bool ShowRecentPickupsOnHud = true;
    public bool ShowLastSessionInsteadOfLastMap = false;
    public bool EnableDebugLogging = false;
    public string ValueUnit = "divine";
    public string CurrencyFilterSearch = string.Empty;
    public List<string> TrackedCurrencyIds = [];
    public Vector2 DrawPosition = new(20f, 400f);
    public Vector4 DefaultBackgroundColor = new(0f, 0f, 0f, 0.85f);
    public Vector4 DefaultFontColor = new(1f, 1f, 1f, 1f);
    public Vector4 AccentColor = new(0.85f, 0.72f, 0.25f, 1f);
    public bool ColorCodeMapValue = true;
    public float MapValueGoodThreshold = 250f;
    public Vector4 GoodMapValueColor = new(0.25f, 0.92f, 0.4f, 1f);
    public Vector4 BadMapValueColor = new(0.95f, 0.28f, 0.28f, 1f);
    public float PickupDistance = 180f;
    public float HudFontSize = 16f;
    public int HudMaxCurrencyLines = 6;
    public int HudMaxRecentPickupLines = 12;
    public int MaxRecentEntries = 25;
}
