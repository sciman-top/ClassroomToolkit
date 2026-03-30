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
}
