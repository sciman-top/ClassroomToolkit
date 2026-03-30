namespace ClassroomToolkit.App.Paint;

internal static class StylusInterpolationDefaults
{
    internal const double MinDtMsForSpeed = 0.2;
    internal const double SpeedNormBase = 0.9;
    internal const double SpeedNormRange = 2.4;
    internal const double StepScaleBase = 0.9;
    internal const double StepScaleSpeedMultiplier = 0.55;
    internal const double InterpolationStepMinDip = 3.0;
    internal const double InterpolationStepMaxDip = 12.0;
    internal const double DistanceTriggerMultiplier = 1.4;

    internal const double FastSpeedThreshold = 3.2;
    internal const double MediumSpeedThreshold = 2.2;
    internal const double SlowSpeedThreshold = 1.4;
    internal const int FastSpeedMaxSegments = 4;
    internal const int MediumSpeedMaxSegments = 5;
    internal const int SlowSpeedMaxSegments = 6;
    internal const int DefaultMaxSegments = 7;
    internal const int MinSegmentCount = 2;
    internal const double SlowFrameDtThresholdMs = 10.0;
    internal const int SlowFrameMaxSegmentsBonus = 1;
    internal const int MaxSegmentsCap = 8;
    internal const double SegmentProgressUpperBound = 1.0;
    internal const int MinTimestampStepTicks = 1;
}
