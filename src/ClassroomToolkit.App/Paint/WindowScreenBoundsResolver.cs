using System;
using System.Drawing;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ClassroomToolkit.App.Paint;

internal static class WindowScreenBoundsResolver
{
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
        if (handle != IntPtr.Zero && NativeMethods.GetWindowRect(handle, out var rect))
        {
            bounds = WindowDipToScreenRectPolicy.ResolveFromScreenRect(
                rect.Left,
                rect.Top,
                rect.Right,
                rect.Bottom);
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
