namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostDriftRepairEnforcePolicy
{
    internal static bool Resolve(ToolbarInteractionRetouchSnapshot snapshot, ToolbarInteractionRetouchTrigger trigger)
    {
        if (trigger != ToolbarInteractionRetouchTrigger.Activated)
        {
            return false;
        }

        if (!InteractiveSceneIntervalPolicy.IsInteractiveScene(
                snapshot.OverlayVisible,
                snapshot.PhotoModeActive,
                snapshot.WhiteboardActive))
        {
            return false;
        }

        return snapshot.LauncherVisible && !snapshot.LauncherTopmost;
    }
}
