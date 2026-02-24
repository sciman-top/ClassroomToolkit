using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BrushRendererPressureTests
{
    [Fact]
    public void MarkerBrushRenderer_ShouldIncreaseAverageWidth_WhenPressureHigher()
    {
        var lowPressureWidth = RenderMarkerAverageWidth(pressure: 0.2, hasPressure: true);
        var highPressureWidth = RenderMarkerAverageWidth(pressure: 0.9, hasPressure: true);

        highPressureWidth.Should().BeGreaterThan(lowPressureWidth);
    }

    [Fact]
    public void MarkerBrushRenderer_ShouldKeepPressureResponseSubtle_ForWhiteboardFeel()
    {
        var lowPressureWidth = RenderMarkerAverageWidth(pressure: 0.2, hasPressure: true);
        var highPressureWidth = RenderMarkerAverageWidth(pressure: 0.9, hasPressure: true);

        var ratio = highPressureWidth / Math.Max(0.001, lowPressureWidth);
        ratio.Should().BeLessThan(1.12);
    }

    [Fact]
    public void VariableWidthBrushRenderer_ShouldIncreaseAverageWidth_WhenPressureHigher()
    {
        var lowPressureWidth = RenderCalligraphyAverageWidth(pressure: 0.2, hasPressure: true);
        var highPressureWidth = RenderCalligraphyAverageWidth(pressure: 0.9, hasPressure: true);

        highPressureWidth.Should().BeGreaterThan(lowPressureWidth);
    }

    [Fact]
    public void VariableWidthBrushRenderer_ShouldFallbackWhenPressureUnavailable()
    {
        var widthWithoutPressure = RenderCalligraphyAverageWidth(pressure: 0.5, hasPressure: false);

        widthWithoutPressure.Should().BeGreaterThan(0);
    }

    private static double RenderMarkerAverageWidth(double pressure, bool hasPressure)
    {
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);

        SimulateStroke(renderer, pressure, hasPressure);

        return renderer.GetLastStrokePoints()!.Average(point => point.Width);
    }

    private static double RenderCalligraphyAverageWidth(double pressure, bool hasPressure)
    {
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        SimulateStroke(renderer, pressure, hasPressure);

        return renderer.GetLastStrokePoints()!.Average(point => point.Width);
    }

    private static void SimulateStroke(IBrushRenderer renderer, double pressure, bool hasPressure)
    {
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(CreateSample(new Point(20, 30), now, pressure, hasPressure));
        for (int i = 1; i <= 12; i++)
        {
            now += step;
            renderer.OnMove(CreateSample(new Point(20 + (i * 6), 30 + (i * 0.8)), now, pressure, hasPressure));
        }
        now += step;
        renderer.OnUp(CreateSample(new Point(96, 39.6), now, pressure, hasPressure));
    }

    private static BrushInputSample CreateSample(Point position, long timestampTicks, double pressure, bool hasPressure)
    {
        return hasPressure
            ? BrushInputSample.CreateStylus(position, timestampTicks, pressure)
            : BrushInputSample.CreatePointer(position, timestampTicks);
    }
}
