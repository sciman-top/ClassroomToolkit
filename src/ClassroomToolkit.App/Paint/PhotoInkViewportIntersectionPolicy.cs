using System.Windows;
using System.Windows.Media;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkViewportIntersectionPolicy
{
    internal static bool ShouldRender(
        bool photoInkModeActive,
        bool usePhotoTransform,
        Rect strokeBounds,
        Matrix photoTransformMatrix,
        Rect viewportBounds)
    {
        if (strokeBounds.IsEmpty || viewportBounds.IsEmpty)
        {
            return false;
        }

        var visibleBounds = photoInkModeActive && usePhotoTransform
            ? Rect.Transform(strokeBounds, photoTransformMatrix)
            : strokeBounds;

        return visibleBounds.IntersectsWith(viewportBounds);
    }
}
