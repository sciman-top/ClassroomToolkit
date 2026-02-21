using System.Windows.Media;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkRenderPolicy
{
    internal static bool ShouldRenderInteractiveInkInPhotoSpace(
        bool photoModeActive,
        Transform? rasterRenderTransform,
        Transform? photoContentTransform)
    {
        return photoModeActive
            && photoContentTransform != null
            && ReferenceEquals(rasterRenderTransform, photoContentTransform);
    }

    internal static bool ShouldRequestImmediateRedraw(
        bool photoModeActive,
        Transform? rasterRenderTransform,
        Transform? photoContentTransform)
    {
        return ShouldRenderInteractiveInkInPhotoSpace(
            photoModeActive,
            rasterRenderTransform,
            photoContentTransform);
    }
}
