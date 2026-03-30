using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class VariableWidthBrushRendererRibbonTests
{
    [Fact]
    public void GetLastRibbonGeometries_ShouldFollowConfiguredLayerCount()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableMultiRibbon = true;
        config.MultiRibbonCount = 3;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        SimulateStroke(renderer, pressure: 0.82, hasPressure: true);

        var ribbons = renderer.GetLastRibbonGeometries();
        ribbons.Should().NotBeNull();
        ribbons!.Count.Should().Be(3);
    }

    [Fact]
    public void GetRibbonOpacity_ShouldDecreaseFromCenterToEdge()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyBalanced();
        config.EnableMultiRibbon = true;
        config.MultiRibbonCount = 3;

        var renderer = new VariableWidthBrushRenderer(config);
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        SimulateStroke(renderer, pressure: 0.9, hasPressure: true);

        var ribbons = renderer.GetLastRibbonGeometries();
        ribbons.Should().NotBeNull();

        var opacityByDistance = ribbons!
            .Select(item => renderer.GetRibbonOpacity(item.RibbonT))
            .OrderByDescending(opacity => opacity)
            .ToList();

        opacityByDistance[0].Should().BeGreaterThan(opacityByDistance[^1]);
    }

    private static void SimulateStroke(IBrushRenderer renderer, double pressure, bool hasPressure)
    {
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);

        renderer.OnDown(CreateSample(new Point(30, 40), now, pressure, hasPressure));
        for (int i = 1; i <= 20; i++)
        {
            now += step;
            var x = 30 + (i * 5.1);
            var y = 40 + (System.Math.Sin(i * 0.35) * 8.0) + (i * 0.8);
            renderer.OnMove(CreateSample(new Point(x, y), now, pressure, hasPressure));
        }
        now += step;
        renderer.OnUp(CreateSample(new Point(138, 73), now, pressure, hasPressure));
    }

    private static BrushInputSample CreateSample(Point position, long timestampTicks, double pressure, bool hasPressure)
    {
        return hasPressure
            ? BrushInputSample.CreateStylus(position, timestampTicks, pressure)
            : BrushInputSample.CreatePointer(position, timestampTicks);
    }
}
