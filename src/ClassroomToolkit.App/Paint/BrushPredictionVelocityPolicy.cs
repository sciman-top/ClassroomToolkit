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

    /// <summary>
    /// 由相邻两次平滑速度的差分估计加速度并做 EMA 平滑 + 幅值钳制，
    /// 仅用于预测预览段的二阶外推项，不进入任何已提交几何。
    /// </summary>
    internal static Vector ResolveAcceleration(
        Vector currentAcceleration,
        BrushInputSample previous,
        BrushInputSample current,
        Vector previousVelocity,
        Vector currentVelocity)
    {
        var dtMs = (current.TimestampTicks - previous.TimestampTicks)
            * 1000.0
            / Math.Max(Stopwatch.Frequency, 1);
        if (dtMs < InkInputRuntimeDefaults.PredictionUpdateMinDtMs)
        {
            return currentAcceleration;
        }

        var dtSeconds = Math.Max(dtMs / 1000.0, BrushPredictionPreviewDefaults.MinPredictionDtSeconds);
        var raw = (currentVelocity - previousVelocity) / dtSeconds;
        var smoothed = new Vector(
            (currentAcceleration.X * BrushPredictionPreviewDefaults.AccelerationKeepFactor)
            + (raw.X * BrushPredictionPreviewDefaults.AccelerationApplyFactor),
            (currentAcceleration.Y * BrushPredictionPreviewDefaults.AccelerationKeepFactor)
            + (raw.Y * BrushPredictionPreviewDefaults.AccelerationApplyFactor));

        double maxMagnitude = BrushPredictionPreviewDefaults.MaxAccelerationDipPerSecSq;
        double magnitudeSq = smoothed.LengthSquared;
        if (magnitudeSq > (maxMagnitude * maxMagnitude))
        {
            smoothed *= maxMagnitude / Math.Sqrt(magnitudeSq);
        }

        return smoothed;
    }
}
