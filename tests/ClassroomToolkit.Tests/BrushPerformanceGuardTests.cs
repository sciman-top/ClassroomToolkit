using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BrushPerformanceGuardTests
{
    public static IEnumerable<object[]> MarkerPerformanceScenarios()
    {
        yield return new object[] { "LongWave", BuildLongWaveTrace(720), 1.35 };
        yield return new object[] { "CornerZigZag", BuildCornerZigZagTrace(760), 1.35 };
        yield return new object[] { "SpiralLoop", BuildSpiralLoopTrace(700), 1.35 };
        yield return new object[] { "SlowMicroJitter", BuildSlowMicroJitterTrace(680), 1.35 };
    }

    public static IEnumerable<object[]> CalligraphyPerformanceScenarios()
    {
        yield return new object[] { "LongWave", BuildLongWaveTrace(720), 1.45 };
        yield return new object[] { "CornerZigZag", BuildCornerZigZagTrace(760), 1.45 };
        yield return new object[] { "SpiralLoop", BuildSpiralLoopTrace(700), 1.45 };
        yield return new object[] { "SlowMicroJitter", BuildSlowMicroJitterTrace(680), 1.45 };
    }

    [Theory]
    [MemberData(nameof(MarkerPerformanceScenarios))]
    public void MarkerResponsiveMode_ShouldStayWithinRelativeCostBudget(
        string scenario,
        IReadOnlyList<StylusPressureSample> trace,
        double maxRatio)
    {
        var ratio = MeasureMedianRelativeCostRatio(
            baselineFactory: () => CreateMarkerRenderer(ClassroomWritingMode.Balanced),
            candidateFactory: () => CreateMarkerRenderer(ClassroomWritingMode.Responsive),
            trace: trace,
            iterations: 7,
            passesPerIteration: 3);

        ratio.Should().BeLessThanOrEqualTo(maxRatio, "scenario: {0}", scenario);
    }

    [Theory]
    [MemberData(nameof(CalligraphyPerformanceScenarios))]
    public void CalligraphyResponsiveMode_ShouldStayWithinRelativeCostBudget(
        string scenario,
        IReadOnlyList<StylusPressureSample> trace,
        double maxRatio)
    {
        var ratio = MeasureMedianRelativeCostRatio(
            baselineFactory: () => CreateCalligraphyRenderer(ClassroomWritingMode.Balanced),
            candidateFactory: () => CreateCalligraphyRenderer(ClassroomWritingMode.Responsive),
            trace: trace,
            iterations: 7,
            passesPerIteration: 3);

        ratio.Should().BeLessThanOrEqualTo(maxRatio, "scenario: {0}", scenario);
    }

    private static MarkerBrushRenderer CreateMarkerRenderer(ClassroomWritingMode mode)
    {
        var config = MarkerBrushConfig.Balanced;
        ClassroomWritingModeTuner.ApplyToMarkerConfig(config, mode);
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, config);
        renderer.Initialize(Colors.DarkBlue, baseSize: 12, opacity: 255);
        return renderer;
    }

    private static VariableWidthBrushRenderer CreateCalligraphyRenderer(ClassroomWritingMode mode)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(config, mode);
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        return renderer;
    }

    private static double MeasureMedianRelativeCostRatio(
        Func<IBrushRenderer> baselineFactory,
        Func<IBrushRenderer> candidateFactory,
        IReadOnlyList<StylusPressureSample> trace,
        int iterations,
        int passesPerIteration)
    {
        for (int i = 0; i < 2; i++)
        {
            var warmupBaseline = baselineFactory();
            ReplayTrace(warmupBaseline, trace, timestampBase: Stopwatch.GetTimestamp());
            var warmupCandidate = candidateFactory();
            ReplayTrace(warmupCandidate, trace, timestampBase: Stopwatch.GetTimestamp());
        }

        var ratios = new List<double>(iterations);
        for (int i = 0; i < iterations; i++)
        {
            var baselineMs = MeasureReplayMs(baselineFactory, trace, passesPerIteration);
            var candidateMs = MeasureReplayMs(candidateFactory, trace, passesPerIteration);
            var ratio = candidateMs / Math.Max(0.1, baselineMs);
            ratios.Add(ratio);
        }

        return ratios.OrderBy(value => value).ElementAt(ratios.Count / 2);
    }

    private static double MeasureReplayMs(
        Func<IBrushRenderer> factory,
        IReadOnlyList<StylusPressureSample> trace,
        int passesPerIteration)
    {
        var sw = Stopwatch.StartNew();
        for (int pass = 0; pass < passesPerIteration; pass++)
        {
            var renderer = factory();
            ReplayTrace(renderer, trace, timestampBase: Stopwatch.GetTimestamp());
        }

        sw.Stop();
        return sw.Elapsed.TotalMilliseconds;
    }

    private static void ReplayTrace(
        IBrushRenderer renderer,
        IReadOnlyList<StylusPressureSample> trace,
        long timestampBase)
    {
        var step = Math.Max(1, Stopwatch.Frequency / 120);
        var timestamp = timestampBase;

        for (int i = 0; i < trace.Count; i++)
        {
            var sample = trace[i];
            var point = new Point(sample.X, sample.Y);
            var input = BrushInputSample.CreateStylus(point, timestamp, sample.Pressure);

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

    private static List<StylusPressureSample> BuildLongWaveTrace(int sampleCount)
    {
        var trace = new List<StylusPressureSample>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double progress = i / (double)(sampleCount - 1);
            double x = 24 + (progress * 1680);
            double y = 320 + (Math.Sin(progress * 12.5) * 46) + (Math.Cos(progress * 4.2) * 18);
            double pressure = 0.2 + (0.7 * (0.5 + (Math.Sin(progress * 9.0) * 0.5)));
            trace.Add(new StylusPressureSample(x, y, Math.Clamp(pressure, 0.05, 0.95)));
        }

        return trace;
    }

    private static List<StylusPressureSample> BuildCornerZigZagTrace(int sampleCount)
    {
        var trace = new List<StylusPressureSample>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double progress = i / (double)(sampleCount - 1);
            double x = 36 + (progress * 1640);

            double phase = progress * 14.0;
            double frac = phase - Math.Floor(phase);
            double triangle = frac <= 0.5 ? frac * 2.0 : (1.0 - frac) * 2.0;
            double y = 340 + ((triangle - 0.5) * 260.0) + (Math.Sin(progress * 28.0) * 6.0);

            double pressure = 0.15 + (0.8 * (0.5 + (Math.Cos(progress * 15.0) * 0.5)));
            trace.Add(new StylusPressureSample(x, y, Math.Clamp(pressure, 0.05, 0.97)));
        }

        return trace;
    }

    private static List<StylusPressureSample> BuildSpiralLoopTrace(int sampleCount)
    {
        var trace = new List<StylusPressureSample>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double progress = i / (double)(sampleCount - 1);
            double angle = progress * Math.PI * 16.0;
            double radius = 36.0 + (progress * 300.0);
            double x = 920 + (Math.Cos(angle) * radius);
            double y = 520 + (Math.Sin(angle) * radius * 0.68);
            double pressure = 0.18 + (0.76 * (0.5 + (Math.Sin(progress * 18.0) * 0.5)));
            trace.Add(new StylusPressureSample(x, y, Math.Clamp(pressure, 0.05, 0.97)));
        }

        return trace;
    }

    private static List<StylusPressureSample> BuildSlowMicroJitterTrace(int sampleCount)
    {
        var trace = new List<StylusPressureSample>(sampleCount);
        for (int i = 0; i < sampleCount; i++)
        {
            double progress = i / (double)(sampleCount - 1);
            double x = 120 + (progress * 420);
            double y = 420 + (Math.Sin(progress * 60.0) * 2.2) + (Math.Cos(progress * 17.0) * 1.3);
            double pressure = 0.32 + (0.36 * (0.5 + (Math.Sin(progress * 11.0) * 0.5)));
            trace.Add(new StylusPressureSample(x, y, Math.Clamp(pressure, 0.08, 0.9)));
        }

        return trace;
    }
}
