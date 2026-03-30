namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostDriftRepairEnforcePolicy
{
    internal static bool Resolve(ToolbarInteractionRetouchSnapshot snapshot, ToolbarInteractionRetouchTrigger trigger)
    {
        if (trigger != ToolbarInteractionRetouchTrigger.Activated)
        {
            return false;
        }

        var interactiveScene = snapshot.OverlayVisible
                               && (snapshot.PhotoModeActive || snapshot.WhiteboardActive);
        if (!interactiveScene)
        {
            return false;
        }

        return snapshot.LauncherVisible && !snapshot.LauncherTopmost;
    }
}
