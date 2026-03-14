namespace ClassroomToolkit.App.Windowing;

internal static class PaintWindowCreationPolicy
{
    internal static bool ShouldEnsureWindows(
        bool hasOverlayWindow,
        bool hasToolbarWindow)
    {
        return !hasOverlayWindow || !hasToolbarWindow;
    }
}
