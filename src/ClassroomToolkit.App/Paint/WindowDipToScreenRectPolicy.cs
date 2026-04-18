using System;
using System.Drawing;

namespace ClassroomToolkit.App.Paint;

internal static class WindowDipToScreenRectPolicy
{
    internal static Rectangle ResolveFromDip(
        double leftDip,
        double topDip,
        double widthDip,
        double heightDip,
        double dpiScaleX,
        double dpiScaleY)
    {
        var scaleX = dpiScaleX > 0 ? dpiScaleX : 1.0;
        var scaleY = dpiScaleY > 0 ? dpiScaleY : 1.0;
        var left = (int)Math.Floor(leftDip * scaleX);
        var top = (int)Math.Floor(topDip * scaleY);
        var width = Math.Max((int)Math.Ceiling(Math.Max(widthDip, 1.0) * scaleX), 1);
        var height = Math.Max((int)Math.Ceiling(Math.Max(heightDip, 1.0) * scaleY), 1);
        return new Rectangle(left, top, width, height);
    }

    internal static Rectangle ResolveFromScreenRect(int left, int top, int right, int bottom)
    {
        var width = Math.Max(right - left, 1);
        var height = Math.Max(bottom - top, 1);
        return new Rectangle(left, top, width, height);
    }
}
