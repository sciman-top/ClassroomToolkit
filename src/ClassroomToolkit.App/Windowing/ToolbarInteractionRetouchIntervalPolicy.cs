namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchIntervalPolicy
{
    internal static int ResolveMs(
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchTrigger trigger,
        int defaultMs = ToolbarInteractionRetouchIntervalDefaults.DefaultMs,
        int interactiveMs = ToolbarInteractionRetouchIntervalDefaults.InteractiveMs)
    {
        if (trigger == ToolbarInteractionRetouchTrigger.PreviewMouseDown)
        {
            return defaultMs;
        }

        return InteractiveSceneIntervalPolicy.ResolveMs(
            snapshot.OverlayVisible,
            snapshot.PhotoModeActive,
            snapshot.WhiteboardActive,
            defaultMs,
            interactiveMs);
    }
}
