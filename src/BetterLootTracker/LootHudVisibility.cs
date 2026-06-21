namespace BetterLootTracker;

using OriathHub;
using OriathHub.RemoteEnums;

internal static class LootHudVisibility
{
    public static bool CanDrawOverlay()
    {
        if (Core.States.GameCurrentState != GameStateTypes.InGameState)
        {
            return false;
        }

        var ui = Core.States.InGameStateObject?.GameUi;
        if (ui is null)
        {
            return true;
        }

        if (ui.IsAnyLargePanelOpen)
        {
            return false;
        }

        if (ui.IsSkillTreeOpen || ui.IsAtlasMapOpen)
        {
            return false;
        }

        if (ui.LeftPanel.IsVisible || ui.RightPanel.IsVisible || ui.WorldMapPanel.IsVisible)
        {
            return false;
        }

        return true;
    }
}
