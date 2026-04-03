using System;
using System.Windows;

namespace ClassroomToolkit.App.Paint;

internal static class InkRedrawClipPolicy
{
    internal static bool ShouldUsePartialClear(
        bool clipAvailable,
        Int32Rect clipPixelRect,
        Int32Rect? lastClipPixelRect)
    {
        return clipAvailable
            && lastClipPixelRect.HasValue
            && lastClipPixelRect.Value.Equals(clipPixelRect);
    }

    internal static bool TryResolvePixelClip(
        Rect clipBoundsDip,
        int surfacePixelWidth,
        int surfacePixelHeight,
        double surfaceDpiX,
        double surfaceDpiY,
        out Int32Rect clipPixelRect)
    {
        clipPixelRect = default;
        if (clipBoundsDip.IsEmpty || clipBoundsDip.Width <= 0 || clipBoundsDip.Height <= 0)
        {
            return false;
        }

        var dpiScaleX = surfaceDpiX > 0 ? surfaceDpiX / 96.0 : 1.0;
        var dpiScaleY = surfaceDpiY > 0 ? surfaceDpiY / 96.0 : 1.0;
        var left = (int)Math.Floor(clipBoundsDip.Left * dpiScaleX);
        var top = (int)Math.Floor(clipBoundsDip.Top * dpiScaleY);
        var right = (int)Math.Ceiling(clipBoundsDip.Right * dpiScaleX);
        var bottom = (int)Math.Ceiling(clipBoundsDip.Bottom * dpiScaleY);

        left = Math.Clamp(left, 0, surfacePixelWidth);
        top = Math.Clamp(top, 0, surfacePixelHeight);
        right = Math.Clamp(right, 0, surfacePixelWidth);
        bottom = Math.Clamp(bottom, 0, surfacePixelHeight);
        var width = right - left;
        var height = bottom - top;
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        clipPixelRect = new Int32Rect(left, top, width, height);
        return true;
    }
}
