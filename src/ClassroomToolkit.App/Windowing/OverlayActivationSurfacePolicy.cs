namespace ClassroomToolkit.App.Windowing;

internal enum OverlayActivationSurfaceReason
{
    None = 0,
    OverlayHidden = 1,
    SurfaceNotSupported = 2
}

internal readonly record struct OverlayActivationSurfaceDecision(
    bool ShouldActivate,
    OverlayActivationSurfaceReason Reason);

internal static class OverlayActivationSurfacePolicy
{
    internal static OverlayActivationSurfaceDecision Resolve(bool overlayVisible, ZOrderSurface frontSurface)
    {
        if (!overlayVisible)
        {
            return new OverlayActivationSurfaceDecision(
                ShouldActivate: false,
                Reason: OverlayActivationSurfaceReason.OverlayHidden);
        }

        var supported = frontSurface is ZOrderSurface.PhotoFullscreen or ZOrderSurface.Whiteboard;
        return supported
            ? new OverlayActivationSurfaceDecision(
                ShouldActivate: true,
                Reason: OverlayActivationSurfaceReason.None)
            : new OverlayActivationSurfaceDecision(
                ShouldActivate: false,
                Reason: OverlayActivationSurfaceReason.SurfaceNotSupported);
    }

    internal static bool ShouldActivate(bool overlayVisible, ZOrderSurface frontSurface)
    {
        return Resolve(overlayVisible, frontSurface).ShouldActivate;
    }
}
