namespace OriathPlugins.Common.Mechanics;

using OriathHub.RemoteObjects.States;

public static class MechanicOverlayVisibility
{
    public static bool ShouldDraw(InGameState inGame)
    {
        var ui = inGame.GameUi;
        if (ui is null)
        {
            return true;
        }

        return !ui.IsAnyLargePanelOpen &&
               !ui.IsSkillTreeOpen &&
               !ui.IsAtlasMapOpen &&
               !ui.LeftPanel.IsVisible &&
               !ui.RightPanel.IsVisible &&
               !ui.WorldMapPanel.IsVisible &&
               !ui.ChatParent.IsChatActive;
    }
}
