using System;

namespace ClassroomToolkit.App.Paint;

internal static class PhotoUnifiedTransformDefaults
{
    internal const double DefaultTranslateDip = 0.0;

    internal static double NormalizeScale(double value)
    {
        return double.IsFinite(value)
            ? Math.Clamp(value, PhotoTransformViewportDefaults.MinScale, PhotoTransformViewportDefaults.MaxScale)
            : PhotoTransformViewportDefaults.DefaultScale;
    }

    internal static double NormalizeTranslation(double value)
    {
        return double.IsFinite(value) ? value : DefaultTranslateDip;
    }
}
