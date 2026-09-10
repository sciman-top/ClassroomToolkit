using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

/// <summary>
/// 毛笔手感包行为契约：
/// 1) 只有 Soft/InkFeel 预设启用压感优先与放宽的动态范围，Clarity/Balanced 保持基线；
/// 2) 有真压感时，同等速度下重按笔画应明显粗于轻提笔画，且对比度强于 Clarity 基线。
/// </summary>
public sealed class BrushPressurePrimaryWidthTests
{
    [Fact]
    public void ClarityPreset_ShouldKeepLegacyWidthRegime()
    {
        var clarity = BrushPhysicsConfig.CreateCalligraphyClarity();
        clarity.PressurePrimaryWidthBlend.Should().Be(0.0);
        clarity.CornerGrowthCapMaxFactor.Should().Be(0.48);
        clarity.MinWidthFactor.Should().Be(0.22);
        clarity.MaxWidthFactor.Should().Be(1.8);
    }

    [Fact]
    public void InkFeelPreset_ShouldEnableBrushFeelPackage()
    {
        var inkFeel = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        inkFeel.PressurePrimaryWidthBlend.Should().BeGreaterThan(0.5);
        inkFeel.CornerGrowthCapMaxFactor.Should().BeGreaterThan(0.48);
        inkFeel.MinWidthFactor.Should().BeLessThan(0.2);
        inkFeel.MaxWidthFactor.Should().BeGreaterThan(2.0);
        inkFeel.MaxStrokeWidthMultiplier.Should().BeGreaterThan(2.8);
        inkFeel.LowSpeedWidthMaxFactor.Should().BeGreaterThan(2.4);
        inkFeel.DunBiMaxAccumulation.Should().BeGreaterThan(1.3);
        inkFeel.StartBurstMaxWidthFactor.Should().BeGreaterThan(1.1);
        inkFeel.TaperEasePower.Should().BeGreaterThan(1.0);
    }

    [Fact]
    public void PressureContrast_ShouldBeStrongerWithPressurePrimaryBlend()
    {
        var (lightClarity, heavyClarity) = ReplayPressurePair(BrushPhysicsConfig.CreateCalligraphyClarity());
        var (lightInkFeel, heavyInkFeel) = ReplayPressurePair(BrushPhysicsConfig.CreateCalligraphyInkFeel());

        double clarityDelta = heavyClarity - lightClarity;
        double inkFeelDelta = heavyInkFeel - lightInkFeel;

        inkFeelDelta.Should().BeGreaterThan(clarityDelta * 1.5,
            "clarity light={0:F2} heavy={1:F2} delta={2:F2}; inkfeel light={3:F2} heavy={4:F2} delta={5:F2}",
            lightClarity, heavyClarity, clarityDelta, lightInkFeel, heavyInkFeel, inkFeelDelta);
        inkFeelDelta.Should().BeGreaterThan(2.0);
    }

    [Fact]
    public void PressurePrimaryWidth_ShouldEnterTheDirectWidthCurveOnlyOnce()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.PressurePrimaryWidthBlend = 1.0;
        config.RealPressureWidthInfluence = 0.0;
        config.WetnessPressureInfluence = 0.0;
        config.WetnessSlowSpeedBoost = 0.0;
        config.DunBiSpreadRate = 0.0;

        const double baseSize = 12.0;
        foreach (var pressure in new[] { 0.12, 0.46, 0.82 })
        {
            var width = ReplayBodyWidth(config, pressure);
            var gamma = Math.Clamp(config.WidthGamma, 0.55, 2.4);
            var expected = baseSize * (
                config.MinWidthFactor
                + ((config.MaxWidthFactor - config.MinWidthFactor)
                    * Math.Pow(pressure, 1.0 / gamma)));

            width.Should().BeApproximately(expected, 0.18,
                "constant pressure {0:F2} should be applied by the single final pressure stage", pressure);
        }
    }

    [Fact]
    public void Renderer_ShouldIgnoreNonFinitePressureSample_WithoutPoisoningTheStroke()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12.0, opacity: 255);

        long timestamp = Stopwatch.GetTimestamp();
        long stepTicks = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(new BrushInputSample(
            new Point(40, 200), timestamp, double.NaN, true));

        timestamp += stepTicks;
        renderer.OnMove(new BrushInputSample(
            new Point(100, 200), timestamp, 0.82, true));
        timestamp += stepTicks;
        renderer.OnUp(new BrushInputSample(
            new Point(160, 200), timestamp, 0.82, true));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThanOrEqualTo(2);
        points.Should().AllSatisfy(point =>
        {
            double.IsFinite(point.Width).Should().BeTrue();
            double.IsFinite(point.Position.X).Should().BeTrue();
            double.IsFinite(point.Position.Y).Should().BeTrue();
        });
        renderer.GetLastCoreGeometry().Should().NotBeNull();
    }

    private static (double LightBodyWidth, double HeavyBodyWidth) ReplayPressurePair(BrushPhysicsConfig config)
    {
        config.EnableRdpSimplify = false;
        return (ReplayBodyWidth(config, 0.15), ReplayBodyWidth(config, 0.95));
    }

    /// <summary>
    /// 以固定中速水平运笔，返回笔迹中段的主体宽度；速度相同、仅压力不同，
    /// 因而宽度差只能来自压感通路。
    /// </summary>
    private static double ReplayBodyWidth(BrushPhysicsConfig config, double pressure)
    {
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12.0, opacity: 255);

        long timestamp = Stopwatch.GetTimestamp();
        long stepTicks = Math.Max(1, Stopwatch.Frequency / 120);
        var point = new Point(40, 200);
        renderer.OnDown(CreateSample(point, timestamp, pressure));
        timestamp += stepTicks;

        for (int i = 1; i <= 48; i++)
        {
            point = new Point(40 + (i * 6.0), 200);
            renderer.OnMove(CreateSample(point, timestamp, pressure));
            timestamp += stepTicks;
        }

        timestamp += stepTicks;
        renderer.OnUp(CreateSample(new Point(point.X + 6.0, 200), timestamp, pressure));

        var samples = renderer.GetLastResampledStrokePointsForDiagnostics();
        samples.Should().NotBeNull();
        samples!.Count.Should().BeGreaterThan(8);

        var body = samples
            .Skip(Math.Max(1, samples.Count / 4))
            .Take(Math.Max(1, samples.Count / 2))
            .Select(sample => sample.Width)
            .OrderBy(width => width)
            .ToList();
        return body[body.Count / 2];
    }

    private static BrushInputSample CreateSample(Point position, long timestampTicks, double pressure)
    {
        return BrushInputSample.CreateStylus(position, timestampTicks, pressure);
    }
}
