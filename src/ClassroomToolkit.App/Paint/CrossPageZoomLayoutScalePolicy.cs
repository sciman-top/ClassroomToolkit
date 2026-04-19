using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageZoomLayoutScalePolicy
{
    internal static bool ShouldSynchronize(double scaleFactor)
    {
        return double.IsFinite(scaleFactor)
            && scaleFactor > 0
            && Math.Abs(scaleFactor - 1.0) >= PhotoZoomInputDefaults.ScaleApplyEpsilon;
    }

    internal static double Scale(double value, double scaleFactor)
    {
        return value * scaleFactor;
    }
}
