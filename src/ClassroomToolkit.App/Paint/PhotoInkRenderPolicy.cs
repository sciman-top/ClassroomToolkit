using System.Windows.Media;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkRenderPolicy
{
    internal static bool ShouldRenderInteractiveInkInPhotoSpace(
        bool photoModeActive,
        Transform? rasterRenderTransform,
        Transform? photoContentTransform)
    {
        // Interactive and persisted ink are rendered into a viewport-sized raster surface.
        // Keep that surface in screen space; otherwise strokes stored in photo coordinates can be
        // clipped away before the photo transform is applied.
        return false;
    }

    internal static bool ShouldRequestImmediateRedraw(
        bool photoModeActive,
        Transform? rasterRenderTransform,
        Transform? photoContentTransform,
        bool crossPageBrushContinuationActive = false)
    {
        return photoModeActive && crossPageBrushContinuationActive;
    }
}
