using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

/// <summary>
/// 倾斜→宽度基线（TiltWidthInfluence）回归测试：
/// 相同轨迹下，笔杆压平（altitude 减小）应得到更宽的笔画；默认关闭时完全不生效。
/// </summary>
public sealed class BrushTiltWidthTests
{
    private const double FlatAltitudeRadians = Math.PI / 6.0;
    private const double UprightAltitudeRadians = Math.PI / 2.0;

    [Fact]
    public void TiltWidthInfluence_FlatterPen_ShouldProduceWiderStroke()
    {
        double flat = ReplayAverageTailWidth(tiltWidthInfluence: 0.35, FlatAltitudeRadians);
        double upright = ReplayAverageTailWidth(tiltWidthInfluence: 0.35, UprightAltitudeRadians);

        flat.Should().BeGreaterThan(upright * 1.1,
            "flat={0:F2} upright={1:F2}", flat, upright);
    }

    [Fact]
    public void TiltWidthInfluence_Zero_ShouldIgnoreTilt()
    {
        double flat = ReplayAverageTailWidth(tiltWidthInfluence: 0.0, FlatAltitudeRadians);
        double upright = ReplayAverageTailWidth(tiltWidthInfluence: 0.0, UprightAltitudeRadians);

        flat.Should().BeApproximately(upright, upright * 0.02 + 0.01,
            "influence=0 flat={0:F2} upright={1:F2}", flat, upright);
    }

    private static double ReplayAverageTailWidth(double tiltWidthInfluence, double altitudeRadians)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyClarity();
        config.TiltWidthInfluence = tiltWidthInfluence;
        config.AnisotropyStrength = 0.0;
        config.EnableOrientationAnisotropy = false;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(System.Windows.Media.Colors.Black, baseSize: 12.0, opacity: 255);

        long timestamp = Stopwatch.GetTimestamp();
        long stepTicks = Math.Max(1, Stopwatch.Frequency / 120);

        var start = new Point(40, 200);
        renderer.OnDown(CreateSample(start, timestamp, altitudeRadians));
        timestamp += stepTicks;

        for (int i = 1; i <= 64; i++)
        {
            double t = i / 64.0;
            var point = new Point(40 + (480.0 * t), 200);
            renderer.OnMove(CreateSample(point, timestamp, altitudeRadians));
            timestamp += stepTicks;
        }

        renderer.OnUp(CreateSample(new Point(530, 200), timestamp, altitudeRadians));

        var samples = renderer.GetLastResampledStrokePointsForDiagnostics();
        samples.Should().NotBeNull();

        double sum = 0;
        int taken = 0;
        int skip = samples!.Count / 2;
        for (int i = skip; i < samples.Count; i++)
        {
            sum += samples[i].Width;
            taken++;
        }

        return taken > 0 ? sum / taken : 0;
    }

    private static BrushInputSample CreateSample(Point position, long timestampTicks, double altitudeRadians)
    {
        return new BrushInputSample(
            position,
            timestampTicks,
            Pressure: 0.6,
            HasPressure: true,
            AzimuthRadians: 0.0,
            AltitudeRadians: altitudeRadians);
    }
}
