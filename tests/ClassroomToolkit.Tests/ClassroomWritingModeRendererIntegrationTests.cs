using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ClassroomWritingModeRendererIntegrationTests
{
    [Fact]
    public void MarkerRenderer_ShouldProduceExpectedModeWidthOrdering()
    {
        var stable = RenderMarkerAverageWidth(ClassroomWritingMode.Stable);
        var balanced = RenderMarkerAverageWidth(ClassroomWritingMode.Balanced);
        var responsive = RenderMarkerAverageWidth(ClassroomWritingMode.Responsive);

        stable.Should().BeLessThan(balanced);
        balanced.Should().BeLessThan(responsive);
    }

    [Fact]
    public void CalligraphyRenderer_ShouldProduceExpectedModeWidthOrdering()
    {
        var stable = RenderCalligraphyAverageWidth(ClassroomWritingMode.Stable);
        var balanced = RenderCalligraphyAverageWidth(ClassroomWritingMode.Balanced);
        var responsive = RenderCalligraphyAverageWidth(ClassroomWritingMode.Responsive);

        stable.Should().BeLessThan(balanced);
        balanced.Should().BeLessThan(responsive);
    }

    private static double RenderMarkerAverageWidth(ClassroomWritingMode mode)
    {
        var config = MarkerBrushConfig.Balanced;
        ClassroomWritingModeTuner.ApplyToMarkerConfig(config, mode);

        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, config);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);
        SimulateStroke(renderer, pressure: 0.9, hasPressure: true);

        return renderer.GetLastStrokePoints()!.Average(point => point.Width);
    }

    private static double RenderCalligraphyAverageWidth(ClassroomWritingMode mode)
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        ClassroomWritingModeTuner.ApplyToCalligraphyConfig(config, mode);

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        SimulateStroke(renderer, pressure: 0.9, hasPressure: true);

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
