namespace BetterLootTracker;

using OriathHub;
using OriathHub.RemoteEnums;

internal static class LootHudVisibility
{
    public static bool CanDrawOverlay(BetterLootTrackerSettings settings)
    {
        if (Core.States.GameCurrentState != GameStateTypes.InGameState)
        {
            return false;
        }

        if (settings.DrawOnlyInHideout)
        {
            var areaDetails = Core.States.InGameStateObject?.CurrentWorldInstance.AreaDetails;
            if (areaDetails is null || !areaDetails.IsHideout)
            {
                return false;
            }
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
