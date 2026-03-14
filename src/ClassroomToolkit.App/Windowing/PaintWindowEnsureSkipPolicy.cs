namespace ClassroomToolkit.App.Windowing;

internal static class PaintWindowEnsureSkipPolicy
{
    internal static bool ShouldSkip(
        bool hasOverlayWindow,
        bool hasToolbarWindow,
        bool eventsWired,
        bool shouldWireOverlayLifecycle,
        bool shouldWireToolbarLifecycle)
    {
        return hasOverlayWindow
               && hasToolbarWindow
               && eventsWired
               && !shouldWireOverlayLifecycle
               && !shouldWireToolbarLifecycle;
    }
}
