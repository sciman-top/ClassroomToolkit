using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class VariableWidthBrushOrientationTests
{
    [Fact]
    public void Renderer_ShouldChangeAverageWidth_WhenAzimuthChanges()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableOrientationAnisotropy = true;
        config.OrientationAnisotropyMix = 1.0;
        config.OrientationAngleOffsetDegrees = 0.0;
        config.AnisotropyStrength = 0.12;

        var widthAlongStroke = RenderAverageWidth(config, azimuthRadians: 0.0, altitudeRadians: 0.5);
        var widthAcrossStroke = RenderAverageWidth(config, azimuthRadians: Math.PI * 0.5, altitudeRadians: 0.5);

        widthAcrossStroke.Should().BeGreaterThan(widthAlongStroke * 1.04);
    }

    [Fact]
    public void Renderer_ShouldApplyStrongerOrientationEffect_WhenPenIsFlatter()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableOrientationAnisotropy = true;
        config.OrientationAnisotropyMix = 1.0;
        config.OrientationAngleOffsetDegrees = 0.0;
        config.AnisotropyStrength = 0.12;

        var flatDelta = Math.Abs(
            RenderAverageWidth(config, azimuthRadians: 0.0, altitudeRadians: 0.2)
            - RenderAverageWidth(config, azimuthRadians: Math.PI * 0.5, altitudeRadians: 0.2));

        var uprightDelta = Math.Abs(
            RenderAverageWidth(config, azimuthRadians: 0.0, altitudeRadians: 1.35)
            - RenderAverageWidth(config, azimuthRadians: Math.PI * 0.5, altitudeRadians: 1.35));

        flatDelta.Should().BeGreaterThan(uprightDelta);
    }

    private static double RenderAverageWidth(
        BrushPhysicsConfig config,
        double azimuthRadians,
        double altitudeRadians)
    {
        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(CreateSample(new Point(20, 30), now, azimuthRadians, altitudeRadians));
        for (int i = 1; i <= 16; i++)
        {
            now += step;
            renderer.OnMove(CreateSample(new Point(20 + (i * 6), 30 + (i * 0.15)), now, azimuthRadians, altitudeRadians));
        }
        now += step;
        renderer.OnUp(CreateSample(new Point(118, 32.4), now, azimuthRadians, altitudeRadians));

        return renderer.GetLastStrokePoints()!.Average(point => point.Width);
    }

    private static BrushInputSample CreateSample(
        Point position,
        long timestampTicks,
        double azimuthRadians,
        double altitudeRadians)
    {
        return BrushInputSample.CreateStylus(
            position,
            timestampTicks,
            pressure: 0.75,
            azimuthRadians: azimuthRadians,
            altitudeRadians: altitudeRadians);
    }
}
