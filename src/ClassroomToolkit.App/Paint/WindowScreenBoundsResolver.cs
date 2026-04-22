using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

internal static class WindowScreenBoundsResolver
{
    private static readonly ICursorWindowGeometryInteropAdapter WindowGeometryAdapter = new NativeCursorWindowGeometryInteropAdapter();

    internal static bool TryResolve(Window? window, out Rectangle bounds, out double dpiScaleX, out double dpiScaleY)
    {
        bounds = Rectangle.Empty;
        dpiScaleX = 1.0;
        dpiScaleY = 1.0;
        if (window == null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(window);
        dpiScaleX = dpi.DpiScaleX > 0 ? dpi.DpiScaleX : 1.0;
        dpiScaleY = dpi.DpiScaleY > 0 ? dpi.DpiScaleY : 1.0;

        var handle = new WindowInteropHelper(window).Handle;
        if (handle != IntPtr.Zero
            && WindowGeometryAdapter.TryGetWindowRect(handle, out var left, out var top, out var right, out var bottom))
        {
            bounds = WindowDipToScreenRectPolicy.ResolveFromScreenRect(
                left,
                top,
                right,
                bottom);
            return true;
        }

        if (!window.IsLoaded && !window.IsVisible)
        {
            return false;
        }

        bounds = WindowDipToScreenRectPolicy.ResolveFromDip(
            window.Left,
            window.Top,
            Math.Max(window.ActualWidth, 1),
            Math.Max(window.ActualHeight, 1),
            dpiScaleX,
            dpiScaleY);
        return true;
    }
}
