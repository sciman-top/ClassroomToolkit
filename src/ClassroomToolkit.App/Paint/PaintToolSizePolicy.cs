using System;

namespace ClassroomToolkit.App.Paint;

internal static class PaintToolSizePolicy
{
    internal const double BrushSizeMinimum = 1.0;
    internal const double BrushSizeDefault = 12.0;
    internal const double EraserSizeMinimum = 4.0;
    internal const double EraserSizeDefault = 24.0;

    internal static double NormalizeBrushSize(double requestedSize, double currentSize)
    {
        return Normalize(requestedSize, currentSize, BrushSizeMinimum, BrushSizeDefault);
    }

    internal static double NormalizeEraserSize(double requestedSize, double currentSize)
    {
        return Normalize(requestedSize, currentSize, EraserSizeMinimum, EraserSizeDefault);
    }

    private static double Normalize(double requestedSize, double currentSize, double minimum, double fallback)
    {
        if (double.IsFinite(requestedSize))
        {
            return Math.Max(minimum, requestedSize);
        }

        return double.IsFinite(currentSize)
            ? Math.Max(minimum, currentSize)
            : fallback;
    }
}
