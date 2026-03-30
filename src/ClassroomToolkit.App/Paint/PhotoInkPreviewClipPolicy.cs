using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkPreviewClipPolicy
{
    internal static Rect ResolveBounds(
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool usePhotoTransform,
        Rect currentPageScreenRect,
        double pageWidthDip,
        double pageHeightDip)
    {
        if (!photoInkModeActive || !crossPageDisplayActive)
        {
            return Rect.Empty;
        }

        if (!currentPageScreenRect.IsEmpty
            && currentPageScreenRect.Width > 0
            && currentPageScreenRect.Height > 0)
        {
            // Preview layer is in screen space; prefer current page screen rect
            // to avoid one-frame cross-page preview bleed at seam transitions.
            return currentPageScreenRect;
        }

        if (!usePhotoTransform || pageWidthDip <= 0 || pageHeightDip <= 0)
        {
            return Rect.Empty;
        }

        return new Rect(0, 0, pageWidthDip, pageHeightDip);
    }
}
