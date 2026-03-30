namespace ClassroomToolkit.App.Windowing;

internal static class PhotoOverlayEntrySurfaceDecisionFactory
{
    internal static SurfaceZOrderDecision Resolve(bool touchPhotoSurface)
    {
        if (!touchPhotoSurface)
        {
            return ForegroundSurfaceDecisionFactory.NoTouch(requestZOrderApply: false);
        }

        var decision = ForegroundSurfaceDecisionFactory.Touch(ZOrderSurface.PhotoFullscreen);
        return decision with { RequestZOrderApply = false };
    }
}
