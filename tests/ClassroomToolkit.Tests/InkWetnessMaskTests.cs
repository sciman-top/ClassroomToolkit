using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

/// <summary>
/// 单笔干湿摘要 → Ink mask 干燥度契约：
/// 1) 旧数据（无湿感字段）保持旧行为；
/// 2) 越写越干的笔画获得更强的纹理变化；
/// 3) 渲染器在书写过程中跟踪起/收/最低含水量。
/// </summary>
public sealed class InkWetnessMaskTests
{
    [Fact]
    public void ResolveDryFactor_ShouldKeepLegacyBehavior_WhenWetnessMissing()
    {
        InkStrokeRenderer.ResolveInkDryFactor(1.0, null, null).Should().Be(0.0);
        InkStrokeRenderer.ResolveInkDryFactor(0.5, null, null).Should().Be(0.5);
        InkStrokeRenderer.ResolveInkDryFactor(0.25, null, null).Should().Be(0.75);
    }

    [Fact]
    public void ResolveDryFactor_ShouldIntensify_WhenStrokeDriesOut()
    {
        InkStrokeRenderer.ResolveInkDryFactor(1.0, 0.9, 0.5).Should().BeApproximately(0.14, 0.001);
        InkStrokeRenderer.ResolveInkDryFactor(0.5, 1.0, 0.0).Should().BeApproximately(0.85, 0.001);
        InkStrokeRenderer.ResolveInkDryFactor(0.6, 0.5, 0.9).Should().Be(0.4,
            "wetness rising along the stroke must not increase dryness");
    }

    [Fact]
    public void ResolveDryFactor_ShouldStayWithinUnitRange()
    {
        InkStrokeRenderer.ResolveInkDryFactor(0.0, 1.0, 0.0).Should().BeLessThanOrEqualTo(1.0);
        InkStrokeRenderer.ResolveInkDryFactor(1.0, 0.0, 1.0).Should().BeGreaterThanOrEqualTo(0.0);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void ResolveInkFlow_ShouldUseFiniteNeutralFallback(double invalidInkFlow)
    {
        InkStrokeRenderer.ResolveInkFlow(invalidInkFlow).Should().Be(0.5);
    }

    [Theory]
    [InlineData(-1.0, 0.0)]
    [InlineData(2.0, 1.0)]
    public void ResolveInkFlow_ShouldClampFiniteValues(double inkFlow, double expected)
    {
        InkStrokeRenderer.ResolveInkFlow(inkFlow).Should().Be(expected);
    }

    [Fact]
    public void Renderer_ShouldTrackStrokeWetnessSummary_AlongSlowStroke()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyClarity();
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12.0, opacity: 255);

        long timestamp = Stopwatch.GetTimestamp();
        long stepTicks = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreateStylus(new Point(40, 200), timestamp, 0.8));
        timestamp += stepTicks;

        for (int i = 1; i <= 40; i++)
        {
            renderer.OnMove(BrushInputSample.CreateStylus(new Point(40 + (i * 1.5), 200), timestamp, 0.8));
            timestamp += stepTicks;
        }

        timestamp += stepTicks;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(110, 200), timestamp, 0.8));

        var summary = renderer.LastStrokeWetnessSummary;
        summary.Start.Should().BeApproximately(config.InitialInkWetness, 0.001);
        summary.Min.Should().BeGreaterThanOrEqualTo(0.08);
        summary.Min.Should().BeLessThanOrEqualTo(summary.Start);
        summary.End.Should().BeInRange(0.08, 1.0);
        summary.End.Should().BeGreaterThan(summary.Min,
            "slow high-pressure ending should be wetter than the driest point of the stroke");
    }
}
