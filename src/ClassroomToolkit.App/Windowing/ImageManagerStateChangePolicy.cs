namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ImageManagerStateChangeDecision(
    bool NormalizeOverlayWindowState,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class ImageManagerStateChangePolicy
{
    internal static ImageManagerStateChangeDecision Resolve(ImageManagerStateChangeContext context)
    {
        return Resolve(
            imageManagerExists: context.ImageManagerExists,
            imageManagerMinimized: context.ImageManagerMinimized,
            overlayVisible: context.OverlayVisible,
            overlayMinimized: context.OverlayMinimized);
    }

    internal static ImageManagerStateChangeDecision Resolve(
        bool imageManagerExists,
        bool imageManagerMinimized,
        bool overlayVisible,
        bool overlayMinimized)
    {
        var shouldRecoverOverlay = imageManagerExists
            && imageManagerMinimized
            && overlayVisible
            && overlayMinimized;

        return new ImageManagerStateChangeDecision(
            NormalizeOverlayWindowState: shouldRecoverOverlay,
            RequestZOrderApply: shouldRecoverOverlay,
            ForceEnforceZOrder: shouldRecoverOverlay);
    }
}
