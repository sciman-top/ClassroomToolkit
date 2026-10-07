
namespace ClassroomToolkit.App.Paint;

internal static class InkInputRuntimeDefaults
{
    internal const double PredictionUpdateMinDtMs = 0.5;
    internal const double RegionSelectionStrokeThicknessDip = 2.0;
    internal const double RegionEraseMinSideDip = 2.0;
    internal const double PhotoReferenceSizeMinDip = 0.5;
}

internal static class InputGeometryDefaults
{
    internal const double MinRenderableImageSideDip = 0.5;
}

internal static class StylusBatchDispatchPolicy
{
    internal const int MinStepTicks = 1;
    internal const int MinSampleCountForDivision = 1;
    internal const int MinBatchOffsetSamples = 0;

    internal static long ResolveStepTicks(long spanTicks, int sampleCount)
    {
        return Math.Max(MinStepTicks, spanTicks / Math.Max(MinSampleCountForDivision, sampleCount));
    }

    internal static long ResolveBatchStartTicks(long nowTicks, long stepTicks, int sampleCount)
    {
        return nowTicks - (stepTicks * Math.Max(MinBatchOffsetSamples, sampleCount - 1));
    }
}

internal static class StylusBatchTimingDefaults
{
    internal const int FallbackHzWhenEmpty = 240;
    internal const int MinPerSampleHz = 480;
    internal const int MaxPerSampleHz = 45;
    internal const int FallbackSpanHz = 120;
}

internal static class StylusBatchTimingPolicy
{
    internal static long ResolveSpanTicks(
        long stopwatchFrequency,
        long nowTicks,
        int sampleCount,
        bool hasPreviousTimestamp,
        long lastTimestampTicks)
    {
        if (sampleCount <= 0)
        {
            return Math.Max(1, stopwatchFrequency / StylusBatchTimingDefaults.FallbackHzWhenEmpty);
        }

        long minPerSampleTicks = Math.Max(1, stopwatchFrequency / StylusBatchTimingDefaults.MinPerSampleHz);
        long maxPerSampleTicks = Math.Max(minPerSampleTicks, stopwatchFrequency / StylusBatchTimingDefaults.MaxPerSampleHz);
        long minSpanTicks = minPerSampleTicks * sampleCount;
        long maxSpanTicks = maxPerSampleTicks * sampleCount;
        long fallbackSpanTicks = Math.Max(
            minSpanTicks,
            (stopwatchFrequency / StylusBatchTimingDefaults.FallbackSpanHz) * sampleCount);

        if (!hasPreviousTimestamp)
        {
            return fallbackSpanTicks;
        }

        long observedSpan = nowTicks - lastTimestampTicks;
        if (observedSpan <= 0)
        {
            return fallbackSpanTicks;
        }

        return Math.Clamp(observedSpan, minSpanTicks, maxSpanTicks);
    }
}

internal enum StylusDownExecutionAction
{
    None = 0,
    HandleFirstStylusPoint = 1,
    HandlePointerPosition = 2
}

internal readonly record struct StylusDownExecutionPlan(
    StylusDownExecutionAction Action,
    bool ShouldResetTimestampState,
    bool ShouldMarkHandled);

internal static class StylusDownExecutionPolicy
{
    internal static StylusDownExecutionPlan Resolve(
        bool photoLoading,
        bool handledByPhotoPan,
        bool shouldIgnoreFromPhotoControls,
        bool hasStylusPoints)
    {
        if (photoLoading || handledByPhotoPan || shouldIgnoreFromPhotoControls)
        {
            return new StylusDownExecutionPlan(
                StylusDownExecutionAction.None,
                ShouldResetTimestampState: false,
                ShouldMarkHandled: false);
        }

        return new StylusDownExecutionPlan(
            hasStylusPoints
                ? StylusDownExecutionAction.HandleFirstStylusPoint
                : StylusDownExecutionAction.HandlePointerPosition,
            ShouldResetTimestampState: true,
            ShouldMarkHandled: true);
    }
}

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

internal enum StylusMoveExecutionAction
{
    None = 0,
    HandlePointerPosition = 1,
    HandleBrushBatch = 2,
    HandleStylusPointsIndividually = 3
}

internal readonly record struct StylusMoveExecutionPlan(
    StylusMoveExecutionAction Action,
    bool ShouldMarkHandled);

internal static class StylusMoveExecutionPolicy
{
    internal static StylusMoveExecutionPlan Resolve(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints,
        PaintToolMode mode,
        bool strokeInProgress,
        bool crossPageDisplayActive)
    {
        if (photoLoading || handledByPhotoPan || !inkOperationActive)
        {
            return new StylusMoveExecutionPlan(
                StylusMoveExecutionAction.None,
                ShouldMarkHandled: false);
        }

        if (!hasStylusPoints)
        {
            return new StylusMoveExecutionPlan(
                StylusMoveExecutionAction.HandlePointerPosition,
                ShouldMarkHandled: true);
        }

        if (mode == PaintToolMode.Brush
            && strokeInProgress
            && !crossPageDisplayActive)
        {
            return new StylusMoveExecutionPlan(
                StylusMoveExecutionAction.HandleBrushBatch,
                ShouldMarkHandled: true);
        }

        return new StylusMoveExecutionPlan(
            StylusMoveExecutionAction.HandleStylusPointsIndividually,
            ShouldMarkHandled: true);
    }
}

internal static class StylusRuntimeDefaults
{
    internal const double PressureGammaStable = 1.16;
    internal const double PressureGammaResponsive = 0.88;
    internal const double PressureGammaDefault = 1.0;

    internal const double CalibratedRangeSeedMinWidth = 0.01;
    internal const double CalibratedLowDefault = 0.0;
    internal const double CalibratedHighDefault = 1.0;
}

internal static class StylusSampleTimestampPolicy
{
    internal static long ResolveBatchSpanTicks(
        long stopwatchFrequency,
        long nowTicks,
        int sampleCount,
        in StylusSampleTimestampState state)
    {
        return StylusBatchTimingPolicy.ResolveSpanTicks(
            stopwatchFrequency,
            nowTicks,
            sampleCount,
            hasPreviousTimestamp: state.HasTimestamp,
            lastTimestampTicks: state.LastTimestampTicks);
    }

    internal static long EnsureMonotonicTimestamp(
        long timestampTicks,
        in StylusSampleTimestampState state)
    {
        if (!state.HasTimestamp)
        {
            return timestampTicks;
        }

        if (timestampTicks <= state.LastTimestampTicks)
        {
            return state.LastTimestampTicks + 1;
        }

        return timestampTicks;
    }
}

internal readonly record struct StylusSampleTimestampState(
    bool HasTimestamp,
    long LastTimestampTicks)
{
    internal static StylusSampleTimestampState Default => new(
        HasTimestamp: false,
        LastTimestampTicks: 0);
}

internal static class StylusSampleTimestampStateUpdater
{
    internal static void Reset(ref StylusSampleTimestampState state)
    {
        state = StylusSampleTimestampState.Default;
    }

    internal static void Remember(
        ref StylusSampleTimestampState state,
        long timestampTicks)
    {
        if (timestampTicks <= 0)
        {
            return;
        }

        state = new StylusSampleTimestampState(
            HasTimestamp: true,
            LastTimestampTicks: timestampTicks);
    }
}

internal enum StylusUpExecutionAction
{
    None = 0,
    HandleLastStylusPoint = 1,
    HandlePointerPosition = 2
}

internal readonly record struct StylusUpExecutionPlan(
    StylusUpExecutionAction Action,
    bool ShouldMarkHandled);

internal static class StylusUpExecutionPolicy
{
    internal static StylusUpExecutionPlan Resolve(
        bool photoLoading,
        bool handledByPhotoPan,
        bool inkOperationActive,
        bool hasStylusPoints)
    {
        if (photoLoading || handledByPhotoPan || !inkOperationActive)
        {
            return new StylusUpExecutionPlan(
                StylusUpExecutionAction.None,
                ShouldMarkHandled: false);
        }

        return new StylusUpExecutionPlan(
            hasStylusPoints
                ? StylusUpExecutionAction.HandleLastStylusPoint
                : StylusUpExecutionAction.HandlePointerPosition,
            ShouldMarkHandled: true);
    }
}
