namespace ClassroomToolkit.App.Paint;

internal static class PhotoContentTransformPolicy
{
    internal static bool ShouldApplyPhotoTransform(
        bool enabledRequested,
        bool photoModeActive,
        bool boardActive,
        bool transformAvailable)
    {
        // RasterImage is backed by a viewport-sized bitmap. Applying the photo transform to that
        // bitmap makes off-viewport photo-space strokes render outside the bitmap and disappear
        // when the page is panned back into view.
        return false;
    }
}
