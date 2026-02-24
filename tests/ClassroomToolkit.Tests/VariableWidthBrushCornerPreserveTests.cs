using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class VariableWidthBrushCornerPreserveTests
{
    [Fact]
    public void OnUp_ShouldKeepSharpCorner_WhenRdpSimplifyEnabled()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableRdpSimplify = true;
        config.RdpEpsilonFactor = 2.5;
        config.RdpMinEpsilon = 40;
        config.RdpCornerPreserveAngleDegrees = 40;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(BrushInputSample.CreateStylus(new Point(10, 10), now, 0.8));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(30, 10), now, 0.8));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(55, 10), now, 0.8));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(55, 35), now, 0.8));
        now += step;
        renderer.OnMove(BrushInputSample.CreateStylus(new Point(55, 55), now, 0.8));
        now += step;
        renderer.OnUp(BrushInputSample.CreateStylus(new Point(55, 72), now, 0.8));

        var points = renderer.GetLastStrokePoints();
        points.Should().NotBeNull();
        points!.Count.Should().BeGreaterThanOrEqualTo(4);
        var geometry = renderer.GetLastStrokeGeometry();
        geometry.Should().NotBeNull();
        geometry!.Bounds.Width.Should().BeGreaterThan(1.0);
        geometry.Bounds.Height.Should().BeGreaterThan(1.0);
    }
}
