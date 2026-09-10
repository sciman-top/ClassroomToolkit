using System;

namespace ClassroomToolkit.App.Paint.Brushes;

internal partial class VariableWidthBrushRenderer
{
    internal bool TryGetPredictionState(out BrushPredictionState state)
    {
        if (_points.Count == 0)
        {
            state = default;
            return false;
        }

        var last = _points[^1];
        state = new BrushPredictionState(
            ClampWidth(last.Width),
            Math.Clamp(_lastPressure, 0.0, 1.0),
            Math.Clamp(last.Wetness, 0.0, 1.0),
            double.IsFinite(last.NibAngleRadians)
                ? last.NibAngleRadians
                : _config.BrushAngleDegrees * Math.PI / 180.0,
            Math.Clamp(last.NibStrength, 0.2, 2.0),
            _hasPressureSample);
        return true;
    }

    internal void ResolvePredictionWidths(
        BrushPredictionState state,
        double speedDipPerSec,
        out double w0,
        out double w1,
        out double w2)
    {
        w0 = ClampWidth(state.Width);
        if (!state.HasPressure)
        {
            w1 = w0;
            w2 = w0;
            return;
        }

        double pressureTarget = CalculatePressureTargetWidth(state.Pressure);
        double pressureBlend = _config.PressurePrimaryWidthBlend > 0.0
            ? Math.Clamp(_config.PressurePrimaryWidthBlend * 0.28, 0.06, 0.34)
            : Math.Clamp(_config.RealPressureWidthInfluence * 0.10, 0.02, 0.12);
        double speedPxPerMs = Math.Max(0.0, speedDipPerSec) / 1000.0;
        double speedNorm = Math.Clamp(
            speedPxPerMs / Math.Max(_config.VelocityThreshold, 0.001),
            0.0,
            1.0);
        double futureResponse = pressureBlend * Lerp(0.65, 1.0, speedNorm);
        double futureWidth = ClampWidth(Lerp(w0, pressureTarget, futureResponse));

        // The first predicted sample starts exactly at the visible endpoint;
        // only the short forecast horizon is allowed to approach the pressure
        // target, so a preview refresh cannot create a width seam.
        w1 = ClampWidth(Lerp(w0, futureWidth, 0.5));
        w2 = futureWidth;
    }
}
