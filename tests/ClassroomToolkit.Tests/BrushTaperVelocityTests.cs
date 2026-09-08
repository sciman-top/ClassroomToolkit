using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

/// <summary>
/// 收锋（end taper）随离笔速度变化的回归测试：
/// 快速离笔应得到更长、更尖的收锋，缓慢停笔应得到更钝的收锋。
/// </summary>
public sealed class BrushTaperVelocityTests
{
    private const double BodyLengthPx = 480.0;
    private const int BodySampleCount = 64;
    private const int TailSampleCount = 12;

    [Fact]
    public void EndTaperLength_ShouldScaleWithReleaseVelocity()
    {
        var fast = ReplayStroke(fastTail: true);
        var slow = ReplayStroke(fastTail: false);

        fast.EffectiveEndTaperLengthDip.Should().BeGreaterThan(
            slow.EffectiveEndTaperLengthDip * 1.3,
            "fast={0:F2}dip slow={1:F2}dip",
            fast.EffectiveEndTaperLengthDip,
            slow.EffectiveEndTaperLengthDip);
    }

    [Fact]
    public void EndTaperLength_ShouldBeVelocityIndependent_WhenFactorsPinned()
    {
        var fast = ReplayStroke(fastTail: true, pinVelocityFactors: true);
        var slow = ReplayStroke(fastTail: false, pinVelocityFactors: true);

        fast.EffectiveEndTaperLengthDip.Should().BeApproximately(
            slow.EffectiveEndTaperLengthDip,
            (slow.EffectiveEndTaperLengthDip * 0.05) + 0.01,
            "pinned fast={0:F2}dip slow={1:F2}dip",
            fast.EffectiveEndTaperLengthDip,
            slow.EffectiveEndTaperLengthDip);
    }

    [Fact]
    public void TailWidthRecovery_ShouldExtendWithFastRelease()
    {
        var fast = ReplayStroke(fastTail: true);
        var slow = ReplayStroke(fastTail: false);

        fast.TailRecoverySpanDip.Should().BeGreaterThan(slow.TailRecoverySpanDip,
            "fast recovery={0:F2}dip slow recovery={1:F2}dip",
            fast.TailRecoverySpanDip,
            slow.TailRecoverySpanDip);
    }

    private static TaperStrokeResult ReplayStroke(bool fastTail, bool pinVelocityFactors = false)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyClarity();
        if (pinVelocityFactors)
        {
            config.TaperLengthVelocityMinFactor = 1.0;
            config.TaperLengthVelocityMaxFactor = 1.0;
        }

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(System.Windows.Media.Colors.Black, baseSize: 12.0, opacity: 255);

        long timestamp = Stopwatch.GetTimestamp();
        long stepTicks = Math.Max(1, Stopwatch.Frequency / 120);

        var startPoint = new Point(40, 200);
        renderer.OnDown(CreateSample(startPoint, timestamp));
        timestamp += stepTicks;

        for (int i = 1; i <= BodySampleCount; i++)
        {
            double t = i / (double)BodySampleCount;
            var point = new Point(
                40 + (BodyLengthPx * t),
                200 + (Math.Sin(t * 2.4) * 6.0));
            renderer.OnMove(CreateSample(point, timestamp));
            timestamp += stepTicks;
        }

        var lastBody = new Point(40 + BodyLengthPx, 200 + (Math.Sin(2.4) * 6.0));
        double tailStepPx = fastTail ? 10.0 : 0.7;
        var tailPoint = lastBody;
        for (int i = 1; i <= TailSampleCount; i++)
        {
            tailPoint = new Point(tailPoint.X + tailStepPx, tailPoint.Y);
            renderer.OnMove(CreateSample(tailPoint, timestamp));
            timestamp += stepTicks;
        }

        renderer.OnUp(CreateSample(new Point(tailPoint.X + tailStepPx, tailPoint.Y), timestamp));

        var samples = renderer.GetLastResampledStrokePointsForDiagnostics();
        samples.Should().NotBeNull();
        samples!.Count.Should().BeGreaterThan(8);

        return new TaperStrokeResult(
            renderer.LastEffectiveEndTaperLengthDip,
            MeasureTailRecoverySpanDip(samples));
    }

    private static BrushInputSample CreateSample(Point position, long timestampTicks)
    {
        return BrushInputSample.CreateStylus(position, timestampTicks, 0.6);
    }

    /// <summary>
    /// 从笔末端向起点回溯，宽度恢复到“肩部宽度 50%”时的弧长距离：
    /// 收锋越长（越尖），该距离越大。
    /// </summary>
    private static double MeasureTailRecoverySpanDip(List<StrokePointData> samples)
    {
        int count = samples.Count;
        var cumulative = new double[count];
        for (int i = 1; i < count; i++)
        {
            cumulative[i] = cumulative[i - 1] + (samples[i].Position - samples[i - 1].Position).Length;
        }

        double total = cumulative[count - 1];
        double shoulder = 0;
        for (int i = 0; i < count; i++)
        {
            double endDistance = total - cumulative[i];
            if (endDistance <= total * 0.35)
            {
                shoulder = Math.Max(shoulder, samples[i].Width);
            }
        }

        double threshold = shoulder * 0.5;
        for (int i = count - 1; i >= 0; i--)
        {
            if (samples[i].Width >= threshold)
            {
                return total - cumulative[i];
            }
        }

        return 0;
    }

    private sealed record TaperStrokeResult(double EffectiveEndTaperLengthDip, double TailRecoverySpanDip);
}
