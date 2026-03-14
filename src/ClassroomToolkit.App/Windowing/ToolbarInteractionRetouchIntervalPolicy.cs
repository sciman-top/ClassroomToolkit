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

        var interactiveScene = snapshot.OverlayVisible
                               && (snapshot.PhotoModeActive || snapshot.WhiteboardActive);
        return interactiveScene ? interactiveMs : defaultMs;
    }
}
