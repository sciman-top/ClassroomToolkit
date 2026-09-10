namespace ClassroomToolkit.App.Paint;

internal static class StylusInterpolationPolicy
{
    internal static double ResolveInterpolationStepDip(double brushSize, double distance, long totalTicks, long stopwatchFrequency)
    {
        double dtMs = totalTicks * 1000.0 / Math.Max(stopwatchFrequency, 1);
        double speedDipPerMs = distance / Math.Max(StylusInterpolationDefaults.MinDtMsForSpeed, dtMs);
        double speedNorm = Math.Clamp(
            (speedDipPerMs - StylusInterpolationDefaults.SpeedNormBase) / StylusInterpolationDefaults.SpeedNormRange,
            0.0,
            1.0);
        double stepScale = StylusInterpolationDefaults.StepScaleBase
            + (speedNorm * StylusInterpolationDefaults.StepScaleSpeedMultiplier);
        return Math.Clamp(
            brushSize * stepScale,
            StylusInterpolationDefaults.InterpolationStepMinDip,
            StylusInterpolationDefaults.InterpolationStepMaxDip);
    }

    internal static bool ShouldInterpolate(double distance, double interpolationStepDip)
    {
        return distance > interpolationStepDip * StylusInterpolationDefaults.DistanceTriggerMultiplier;
    }

    internal static int ResolveMaxSegments(double speedDipPerMs, double dtMs)
    {
        int maxSegments = speedDipPerMs switch
        {
            > StylusInterpolationDefaults.FastSpeedThreshold => StylusInterpolationDefaults.FastSpeedMaxSegments,
            > StylusInterpolationDefaults.MediumSpeedThreshold => StylusInterpolationDefaults.MediumSpeedMaxSegments,
            > StylusInterpolationDefaults.SlowSpeedThreshold => StylusInterpolationDefaults.SlowSpeedMaxSegments,
            _ => StylusInterpolationDefaults.DefaultMaxSegments
        };

        if (dtMs > StylusInterpolationDefaults.SlowFrameDtThresholdMs)
        {
            maxSegments = Math.Min(
                StylusInterpolationDefaults.MaxSegmentsCap,
                maxSegments + StylusInterpolationDefaults.SlowFrameMaxSegmentsBonus);
        }

        return maxSegments;
    }

    internal static double? LerpNullableAngle(double? a, double? b, double t)
    {
        if (!a.HasValue && !b.HasValue)
        {
            return null;
        }

        double from = NormalizeAngle(a ?? b ?? 0.0);
        double to = NormalizeAngle(b ?? a ?? 0.0);
        return LerpAngle(from, to, t);
    }

    internal static double LerpAngle(double start, double end, double t)
    {
        double delta = NormalizeAngle(end - start);
        if (delta > Math.PI)
        {
            delta -= Math.PI * 2.0;
        }

        return NormalizeAngle(start + (delta * Math.Clamp(t, 0.0, 1.0)));
    }

    private static double NormalizeAngle(double angle)
    {
        while (angle <= -Math.PI)
        {
            angle += Math.PI * 2.0;
        }
        while (angle > Math.PI)
        {
            angle -= Math.PI * 2.0;
        }

        return angle;
    }
}
