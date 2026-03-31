using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoInkCurrentPageClipPolicy
{
    internal static Rect ResolveBounds(
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool photoFullscreenActive,
        bool usePhotoTransform,
        Rect currentPageScreenRect,
        double pageWidthDip,
        double pageHeightDip)
    {
        if (!photoInkModeActive
            || !crossPageDisplayActive
            || photoFullscreenActive)
        {
            return Rect.Empty;
        }

        if (usePhotoTransform)
        {
            if (pageWidthDip <= 0 || pageHeightDip <= 0)
            {
                return Rect.Empty;
            }

            return new Rect(0, 0, pageWidthDip, pageHeightDip);
        }

        if (currentPageScreenRect.IsEmpty
            || currentPageScreenRect.Width <= 0
            || currentPageScreenRect.Height <= 0)
        {
            return Rect.Empty;
        }

        return currentPageScreenRect;
    }
}
