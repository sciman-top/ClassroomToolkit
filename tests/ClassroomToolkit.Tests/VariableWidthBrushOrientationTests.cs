using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

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

    [Fact]
    public void Renderer_ShouldMoveExposedEndTip_WhenEndpointOrientationChanges()
    {
        var horizontalNib = RenderExposedGeometry(azimuthRadians: 0.0);
        var verticalNib = RenderExposedGeometry(azimuthRadians: Math.PI * 0.5);

        AssertFiniteNonEmpty(horizontalNib.Geometry);
        AssertFiniteNonEmpty(verticalNib.Geometry);

        var horizontalTip = GetForwardmostPoint(horizontalNib.Geometry);
        var verticalTip = GetForwardmostPoint(verticalNib.Geometry);

        double tipDelta = Math.Abs(horizontalTip.Y - verticalTip.Y);
        tipDelta.Should().BeGreaterThan(
            0.45,
            "the exposed cap must respond to endpoint nib orientation, not only change body width");
    }

    [Fact]
    public void Renderer_ShouldFallBackToFiniteGeometry_WhenOrientationIsNotFinite()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.EnableMultiRibbon = false;
        config.TiltWidthInfluence = 0.35;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(new BrushInputSample(
            new Point(20, 60), now, 0.8, true, double.NaN, double.NaN));
        now += step;
        renderer.OnMove(new BrushInputSample(
            new Point(100, 60), now, 0.8, true, double.PositiveInfinity, double.NegativeInfinity));
        now += step;
        renderer.OnUp(new BrushInputSample(
            new Point(180, 60), now, 0.8, true, double.NaN, double.NaN));

        var geometry = renderer.GetLastCoreGeometry();
        geometry.Should().NotBeNull();
        AssertFiniteNonEmpty(geometry!);
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

    private static (Geometry Geometry, Point Release) RenderExposedGeometry(double azimuthRadians)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyInkFeel();
        config.EnableRdpSimplify = false;
        config.EnableMultiRibbon = false;
        config.MultiRibbonCount = 1;
        config.OrientationAnisotropyMix = 1.0;
        config.AnisotropyStrength = 0.0;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(CreateSample(new Point(24, 120), now, azimuthRadians, 0.55));
        for (int i = 1; i <= 48; i++)
        {
            now += step;
            renderer.OnMove(CreateSample(new Point(24 + (i * 4.5), 120), now, azimuthRadians, 0.55));
        }

        var release = new Point(252, 120);
        now += step;
        renderer.OnUp(CreateSample(release, now, azimuthRadians, 0.55));

        var geometry = renderer.GetLastCoreGeometry();
        geometry.Should().NotBeNull();
        return (geometry!, release);
    }

    private static void AssertFiniteNonEmpty(Geometry geometry)
    {
        var bounds = geometry.Bounds;
        bounds.IsEmpty.Should().BeFalse();
        bounds.Width.Should().BeGreaterThan(0.0);
        bounds.Height.Should().BeGreaterThan(0.0);
        double.IsFinite(bounds.X).Should().BeTrue();
        double.IsFinite(bounds.Y).Should().BeTrue();
        double.IsFinite(bounds.Width).Should().BeTrue();
        double.IsFinite(bounds.Height).Should().BeTrue();
    }

    private static Point GetForwardmostPoint(Geometry geometry)
    {
        var flattened = geometry.GetFlattenedPathGeometry(0.05, ToleranceType.Absolute);
        var points = flattened.Figures.SelectMany(EnumerateFigurePoints).ToList();
        points.Should().NotBeEmpty();
        return points.OrderByDescending(point => point.X).First();
    }

    private static IEnumerable<Point> EnumerateFigurePoints(PathFigure figure)
    {
        yield return figure.StartPoint;
        foreach (var segment in figure.Segments)
        {
            switch (segment)
            {
                case LineSegment line:
                    yield return line.Point;
                    break;
                case PolyLineSegment polyLine:
                    foreach (var point in polyLine.Points)
                    {
                        yield return point;
                    }
                    break;
            }
        }
    }
}
