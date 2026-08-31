using System.Diagnostics;
using System.Windows;
using ClassroomToolkit.App.Paint.Brushes;

namespace ClassroomToolkit.App.Paint;

internal static class BrushPredictionVelocityPolicy
{
    internal static Vector Resolve(
        Vector currentVelocity,
        BrushInputSample previous,
        BrushInputSample current)
    {
        var dtMs = (current.TimestampTicks - previous.TimestampTicks)
            * 1000.0
            / Math.Max(Stopwatch.Frequency, 1);
        if (dtMs < InkInputRuntimeDefaults.PredictionUpdateMinDtMs)
        {
            return currentVelocity;
        }

        var dtSeconds = dtMs / 1000.0;
        var measured = (current.Position - previous.Position)
            / Math.Max(dtSeconds, BrushPredictionPreviewDefaults.MinPredictionDtSeconds);
        return new Vector(
            (currentVelocity.X * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (measured.X * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor),
            (currentVelocity.Y * BrushPredictionPreviewDefaults.VelocitySmoothingKeepFactor)
            + (measured.Y * BrushPredictionPreviewDefaults.VelocitySmoothingApplyFactor));
    }
}
