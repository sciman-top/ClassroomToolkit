namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionActivationSuppressionWindowPolicy
{
    internal static int ResolveMs(
        ToolbarInteractionRetouchSnapshot snapshot,
        int defaultMs = ToolbarInteractionActivationSuppressionDefaults.LauncherOnlyAfterPreviewSuppressionMs,
        int interactiveMs = ToolbarInteractionActivationSuppressionDefaults.LauncherOnlyAfterPreviewInteractiveSuppressionMs)
    {
        if (snapshot.PhotoModeActive || snapshot.WhiteboardActive)
        {
            return interactiveMs;
        }

        return defaultMs;
    }
}
