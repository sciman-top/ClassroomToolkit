using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ClassroomWritingModeStylusReplayTests
{
    [Fact]
    public void Replay_ShouldDowngradeLegacyPseudoPressureToPointerForAllModes()
    {
        var stable = ResolveTrace(StylusPressureReplayFixture.LegacyPseudoPressureTrace, ClassroomWritingMode.Stable);
        var balanced = ResolveTrace(StylusPressureReplayFixture.LegacyPseudoPressureTrace, ClassroomWritingMode.Balanced);
        var responsive = ResolveTrace(StylusPressureReplayFixture.LegacyPseudoPressureTrace, ClassroomWritingMode.Responsive);

        stable.StylusCount.Should().Be(0);
        balanced.StylusCount.Should().Be(0);
        responsive.StylusCount.Should().Be(0);

        stable.PointerCount.Should().Be(StylusPressureReplayFixture.LegacyPseudoPressureTrace.Count);
        balanced.PointerCount.Should().Be(StylusPressureReplayFixture.LegacyPseudoPressureTrace.Count);
        responsive.PointerCount.Should().Be(StylusPressureReplayFixture.LegacyPseudoPressureTrace.Count);
    }

    [Fact]
    public void Replay_ShouldApplyModeSpecificThresholdSegmentation_OnEdgeTrace()
    {
        var stable = ResolveTrace(StylusPressureReplayFixture.ThresholdEdgeTrace, ClassroomWritingMode.Stable);
        var balanced = ResolveTrace(StylusPressureReplayFixture.ThresholdEdgeTrace, ClassroomWritingMode.Balanced);
        var responsive = ResolveTrace(StylusPressureReplayFixture.ThresholdEdgeTrace, ClassroomWritingMode.Responsive);

        stable.StylusCount.Should().Be(0);
        balanced.StylusCount.Should().Be(4);
        responsive.StylusCount.Should().Be(6);
    }

    [Fact]
    public void MarkerReplay_ShouldRemainModeInvariant_WhenTraceIsPseudoPressureOnly()
    {
        var stable = RenderMarkerAverageWidth(StylusPressureReplayFixture.LegacyPseudoPressureTrace, ClassroomWritingMode.Stable);
        var balanced = RenderMarkerAverageWidth(StylusPressureReplayFixture.LegacyPseudoPressureTrace, ClassroomWritingMode.Balanced);
        var responsive = RenderMarkerAverageWidth(StylusPressureReplayFixture.LegacyPseudoPressureTrace, ClassroomWritingMode.Responsive);

        stable.Should().BeApproximately(balanced, 0.0001);
        balanced.Should().BeApproximately(responsive, 0.0001);
    }

    [Fact]
    public void MarkerReplay_ShouldKeepModeWidthOrdering_WhenTraceHasContinuousPressure()
    {
        var stable = RenderMarkerAverageWidth(StylusPressureReplayFixture.ModernContinuousTrace, ClassroomWritingMode.Stable);
        var balanced = RenderMarkerAverageWidth(StylusPressureReplayFixture.ModernContinuousTrace, ClassroomWritingMode.Balanced);
        var responsive = RenderMarkerAverageWidth(StylusPressureReplayFixture.ModernContinuousTrace, ClassroomWritingMode.Responsive);

        stable.Should().BeLessThan(balanced);
        balanced.Should().BeLessThan(responsive);
    }

    [Fact]
    public void CalligraphyReplay_ShouldRemainModeSensitive_WhenTraceHasContinuousPressure()
    {
        var stable = RenderCalligraphyAverageWidth(StylusPressureReplayFixture.ModernContinuousTrace, ClassroomWritingMode.Stable);
        var balanced = RenderCalligraphyAverageWidth(StylusPressureReplayFixture.ModernContinuousTrace, ClassroomWritingMode.Balanced);
        var responsive = RenderCalligraphyAverageWidth(StylusPressureReplayFixture.ModernContinuousTrace, ClassroomWritingMode.Responsive);

        stable.Should().NotBeApproximately(balanced, 0.01);
        responsive.Should().NotBeApproximately(balanced, 0.01);
    }

    private static (int StylusCount, int PointerCount) ResolveTrace(
        IReadOnlyList<StylusPressureSample> trace,
        ClassroomWritingMode mode)
    {
        var runtime = ClassroomWritingModeTuner.ResolveRuntimeSettings(mode);
        var stylusCount = 0;
        var pointerCount = 0;

        foreach (var sample in trace)
        {
            var accepted = ClassroomWritingModeTuner.TryResolveStylusPressure(
                sample.Pressure,
                runtime.PseudoPressureLowThreshold,
                runtime.PseudoPressureHighThreshold,
                out _);
            if (accepted)
            {
                stylusCount++;
            }
            else
            {
                pointerCount++;
            }
        }

        return (stylusCount, pointerCount);
    }

    private static double RenderMarkerAverageWidth(
        IReadOnlyList<StylusPressureSample> trace,
        ClassroomWritingMode mode)
    {
        var config = MarkerBrushConfig.Balanced;
        ClassroomWritingModeTuner.ApplyToMarkerConfig(config, mode);

        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, config);
        renderer.Initialize(Colors.Blue, baseSize: 12, opacity: 255);
        ReplayTrace(renderer, trace, mode);

        return renderer.GetLastStrokePoints()!.Average(point => point.Width);
    }

    private static double RenderCalligraphyAverageWidth(
        IReadOnlyList<StylusPressureSample> trace,
        ClassroomWritingMode mode)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(config, mode);

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        ReplayTrace(renderer, trace, mode);

        return renderer.GetLastStrokePoints()!.Average(point => point.Width);
    }

    private static void ReplayTrace(
        IBrushRenderer renderer,
        IReadOnlyList<StylusPressureSample> trace,
        ClassroomWritingMode mode)
    {
        trace.Count.Should().BeGreaterThanOrEqualTo(2);

        var runtime = ClassroomWritingModeTuner.ResolveRuntimeSettings(mode);
        var timestamp = Stopwatch.GetTimestamp();
        var step = Math.Max(1, Stopwatch.Frequency / 120);

        for (var i = 0; i < trace.Count; i++)
        {
            var input = CreateInput(trace[i], timestamp, runtime);
            if (i == 0)
            {
                renderer.OnDown(input);
            }
            else if (i == trace.Count - 1)
            {
                renderer.OnUp(input);
            }
            else
            {
                renderer.OnMove(input);
            }

            timestamp += step;
        }
    }

    private static BrushInputSample CreateInput(
        StylusPressureSample sample,
        long timestampTicks,
        ClassroomRuntimeSettings runtime)
    {
        var position = new Point(sample.X, sample.Y);
        var hasStylusPressure = ClassroomWritingModeTuner.TryResolveStylusPressure(
            sample.Pressure,
            runtime.PseudoPressureLowThreshold,
            runtime.PseudoPressureHighThreshold,
            out var pressure);

        return hasStylusPressure
            ? BrushInputSample.CreateStylus(position, timestampTicks, pressure)
            : BrushInputSample.CreatePointer(position, timestampTicks);
    }
}
