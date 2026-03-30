namespace ClassroomToolkit.App.Windowing;

internal static class PhotoOverlayEntrySurfaceTransitionPolicy
{
    internal static SurfaceZOrderDecision Resolve(bool touchPhotoSurface)
    {
        return PhotoOverlayEntrySurfaceDecisionFactory.Resolve(touchPhotoSurface);
    }
}
