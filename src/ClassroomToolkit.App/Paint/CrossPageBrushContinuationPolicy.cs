using System;
using ClassroomToolkit.App.Paint.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageBrushContinuationPolicy
{
    private const double ContinuationInPageOffsetDipMin = CrossPageBrushContinuationDefaults.InPageOffsetDipMin;
    private const double ContinuationInPageOffsetDipMax = CrossPageBrushContinuationDefaults.InPageOffsetDipMax;
    private const double ContinuationInPageOffsetFactor = CrossPageBrushContinuationDefaults.InPageOffsetFactor;

    internal readonly record struct Decision(
        BrushInputSample FinalizeSample,
        BrushInputSample ContinuationSeed,
        bool ShouldReplayCurrentInputAfterResume);

    internal static Decision Resolve(
        BrushInputSample currentInput,
        BrushInputSample? previousInput,
        double currentPageTop,
        double currentPageHeight,
        int currentPage,
        int targetPage)
    {
        if (!previousInput.HasValue || targetPage == currentPage || currentPageHeight <= 0)
        {
            return new Decision(currentInput, currentInput, false);
        }

        var seamY = targetPage > currentPage
            ? currentPageTop + currentPageHeight
            : currentPageTop;
        if (!TryCreateBridgeSample(previousInput.Value, currentInput, seamY, out var bridgeSample))
        {
            return new Decision(currentInput, currentInput, false);
        }

        var continuationSeed = CreateContinuationSeed(bridgeSample, currentInput, targetPage > currentPage);
        var shouldReplayCurrent = !AreClose(
            continuationSeed.Position,
            currentInput.Position,
            toleranceDip: CrossPageBrushContinuationDefaults.ReplayDistanceToleranceDip);
        return new Decision(bridgeSample, continuationSeed, shouldReplayCurrent);
    }

    private static bool TryCreateBridgeSample(
        BrushInputSample from,
        BrushInputSample to,
        double seamY,
        out BrushInputSample bridgeSample)
    {
        bridgeSample = to;
        var fromY = from.Position.Y;
        var toY = to.Position.Y;
        var deltaY = toY - fromY;
        if (Math.Abs(deltaY) < CrossPageBrushContinuationBridgeDefaults.DeltaYEpsilon)
        {
            return false;
        }

        var t = (seamY - fromY) / deltaY;
        if (t <= CrossPageBrushContinuationBridgeDefaults.InterpolationLowerExclusive
            || t >= CrossPageBrushContinuationBridgeDefaults.InterpolationUpperExclusive)
        {
            return false;
        }

        var bridgePosition = new WpfPoint(
            from.Position.X + ((to.Position.X - from.Position.X) * t),
            seamY);

        var bridgeTicks = to.TimestampTicks;
        if (to.TimestampTicks > from.TimestampTicks)
        {
            var span = to.TimestampTicks - from.TimestampTicks;
            bridgeTicks = from.TimestampTicks + (long)Math.Round(span * t, MidpointRounding.AwayFromZero);
        }

        var hasPressure = to.HasPressure || from.HasPressure;
        var pressure = to.Pressure;
        if (from.HasPressure && to.HasPressure)
        {
            pressure = from.Pressure + ((to.Pressure - from.Pressure) * t);
        }
        else if (from.HasPressure && !to.HasPressure)
        {
            pressure = from.Pressure;
        }

        bridgeSample = new BrushInputSample(
            bridgePosition,
            bridgeTicks,
            pressure,
            hasPressure,
            to.AzimuthRadians,
            to.AltitudeRadians,
            to.TiltXRadians,
            to.TiltYRadians);
        return true;
    }

    private static BrushInputSample CreateContinuationSeed(
        BrushInputSample bridgeSample,
        BrushInputSample currentInput,
        bool forwardToNextPage)
    {
        var direction = forwardToNextPage ? 1.0 : -1.0;
        var offset = ResolveContinuationOffsetDip(
            currentInput.Position.Y - bridgeSample.Position.Y);
        var seedPosition = new WpfPoint(
            bridgeSample.Position.X,
            bridgeSample.Position.Y + (offset * direction));

        return new BrushInputSample(
            seedPosition,
            bridgeSample.TimestampTicks + CrossPageBrushContinuationBridgeDefaults.SeedTimestampIncrementTicks,
            bridgeSample.Pressure,
            bridgeSample.HasPressure,
            bridgeSample.AzimuthRadians,
            bridgeSample.AltitudeRadians,
            bridgeSample.TiltXRadians,
            bridgeSample.TiltYRadians);
    }

    private static double ResolveContinuationOffsetDip(double deltaYFromSeam)
    {
        var adaptiveOffset = Math.Abs(deltaYFromSeam) * ContinuationInPageOffsetFactor;
        return Math.Clamp(
            adaptiveOffset,
            ContinuationInPageOffsetDipMin,
            ContinuationInPageOffsetDipMax);
    }

    private static bool AreClose(WpfPoint a, WpfPoint b, double toleranceDip)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return (dx * dx) + (dy * dy) <= toleranceDip * toleranceDip;
    }
}
