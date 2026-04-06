using System.Windows;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoZoomAnchorPolicy
{
    internal static WpfPoint ResolveViewportCenter(FrameworkElement? viewport)
    {
        if (viewport == null)
        {
            return default;
        }

        return ResolveViewportCenter(viewport.ActualWidth, viewport.ActualHeight);
    }

    internal static WpfPoint ResolveViewportCenter(double viewportWidth, double viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return default;
        }

        return new WpfPoint(viewportWidth * 0.5, viewportHeight * 0.5);
    }
}
