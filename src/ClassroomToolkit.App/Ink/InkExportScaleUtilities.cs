using System;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.Ink;

internal static class InkExportScaleUtilities
{
    internal static double ResolveScale(double target, double reference, double fallback)
    {
        if (reference > 0.5)
        {
            return target / reference;
        }

        if (Math.Abs(fallback) > 0.0001)
        {
            return fallback;
        }

        return 1.0;
    }

    internal static double GetBitmapWidthDip(BitmapSource bitmap)
    {
        if (bitmap.Width > 0)
        {
            return bitmap.Width;
        }

        var dpiX = bitmap.DpiX > 0 ? bitmap.DpiX : 96.0;
        return bitmap.PixelWidth * 96.0 / dpiX;
    }

    internal static double GetBitmapHeightDip(BitmapSource bitmap)
    {
        if (bitmap.Height > 0)
        {
            return bitmap.Height;
        }

        var dpiY = bitmap.DpiY > 0 ? bitmap.DpiY : 96.0;
        return bitmap.PixelHeight * 96.0 / dpiY;
    }
}

