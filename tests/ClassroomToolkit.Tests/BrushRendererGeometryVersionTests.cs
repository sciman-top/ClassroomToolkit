using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BrushRendererGeometryVersionTests
{
    [Fact]
    public void MarkerRenderer_GeometryVersion_ShouldStayWhenMoveIgnored()
    {
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(10, 10), now));
        var versionAfterDown = renderer.GeometryVersion;

        renderer.OnMove(BrushInputSample.CreatePointer(new Point(10.1, 10.1), now + Math.Max(1, Stopwatch.Frequency / 120)));
        renderer.GeometryVersion.Should().Be(versionAfterDown);
    }

    [Fact]
    public void MarkerRenderer_GeometryVersion_ShouldIncreaseWhenPointAppended()
    {
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(10, 10), now));
        var versionAfterDown = renderer.GeometryVersion;

        renderer.OnMove(BrushInputSample.CreatePointer(new Point(16, 11), now + Math.Max(1, Stopwatch.Frequency / 120)));
        renderer.GeometryVersion.Should().BeGreaterThan(versionAfterDown);
    }

    [Fact]
    public void CalligraphyRenderer_GeometryVersion_ShouldStayWhenMoveIgnored()
    {
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(20, 20), now));
        var versionAfterDown = renderer.GeometryVersion;

        renderer.OnMove(BrushInputSample.CreatePointer(new Point(20.1, 20.1), now + Math.Max(1, Stopwatch.Frequency / 120)));
        renderer.GeometryVersion.Should().Be(versionAfterDown);
    }

    [Fact]
    public void CalligraphyRenderer_GeometryVersion_ShouldIncreaseWhenPointAppended()
    {
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(20, 20), now));
        var versionAfterDown = renderer.GeometryVersion;

        renderer.OnMove(BrushInputSample.CreatePointer(new Point(29, 26), now + Math.Max(1, Stopwatch.Frequency / 120)));
        renderer.GeometryVersion.Should().BeGreaterThan(versionAfterDown);
    }

    [Fact]
    public void CalligraphyRenderer_GeometryVersion_ShouldIncreaseWhenLargeMoveHasFrameDelay()
    {
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(20, 20), now));
        var versionAfterDown = renderer.GeometryVersion;
        long delayedTicks = now + Math.Max(1, (long)Math.Round(Stopwatch.Frequency * 0.08));

        renderer.OnMove(BrushInputSample.CreatePointer(new Point(800, 24), delayedTicks));
        renderer.GeometryVersion.Should().BeGreaterThan(versionAfterDown);
    }

    [Fact]
    public void CalligraphyRenderer_GeometryVersion_ShouldIgnoreLargeMoveWhenIntervalTooShort()
    {
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(20, 20), now));
        var versionAfterDown = renderer.GeometryVersion;
        long shortTicks = now + Math.Max(1, (long)Math.Round(Stopwatch.Frequency * 0.002));

        renderer.OnMove(BrushInputSample.CreatePointer(new Point(800, 24), shortTicks));
        renderer.GeometryVersion.Should().Be(versionAfterDown);
    }
}
