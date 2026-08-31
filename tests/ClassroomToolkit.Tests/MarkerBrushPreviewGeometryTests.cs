using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MarkerBrushPreviewGeometryTests
{
    public static TheoryData<int> WhiteboardPresets => new()
    {
        0,
        1,
        2
    };

    public static TheoryData<int, int, double> ClassroomSpeedScenarios => new()
    {
        { 0, 60, 12.0 },
        { 1, 60, 12.0 },
        { 2, 60, 12.0 },
        { 0, 120, 10.0 },
        { 1, 120, 10.0 },
        { 2, 120, 10.0 }
    };

    [Fact]
    public void RibbonRenderer_Preview_ShouldUseSingleStreamGeometry()
    {
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);
        long timestamp = Stopwatch.GetTimestamp();
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(10, 10), timestamp));
        renderer.OnMove(BrushInputSample.CreatePointer(new Point(30, 14), timestamp + Stopwatch.Frequency / 120));
        renderer.OnMove(BrushInputSample.CreatePointer(new Point(50, 22), timestamp + (Stopwatch.Frequency / 60)));

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            renderer.Render(dc);
        }

        var group = visual.Drawing.Should().BeOfType<DrawingGroup>().Subject;
        var geometryDrawing = group.Children.Should().ContainSingle().Which.Should().BeOfType<GeometryDrawing>().Subject;
        geometryDrawing.Geometry.Should().BeOfType<StreamGeometry>();
    }

    [Fact]
    public void LongRibbonPreview_ShouldKeepGeometryBoundedToBaseAndTail()
    {
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);
        long timestamp = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        renderer.OnDown(BrushInputSample.CreatePointer(new Point(10, 40), timestamp));
        for (int i = 1; i <= 160; i++)
        {
            timestamp += step;
            renderer.OnMove(BrushInputSample.CreatePointer(
                new Point(10 + (i * 4.0), 40 + (Math.Sin(i * 0.2) * 8.0)),
                timestamp));
        }

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            renderer.Render(dc);
        }

        var drawing = visual.Drawing.Should().BeOfType<DrawingGroup>().Subject;
        var geometryDrawing = drawing.Children.Should().ContainSingle().Which.Should().BeOfType<GeometryDrawing>().Subject;
        var preview = geometryDrawing.Geometry.Should().BeOfType<GeometryGroup>().Subject;
        preview.Children.Should().HaveCount(2);
        preview.Children.Should().OnlyContain(geometry => geometry is StreamGeometry);
    }

    [Theory]
    [MemberData(nameof(WhiteboardPresets))]
    public void FastPointerMove_ShouldKeepAcceptedTipCloseToPointer(int preset)
    {
        var config = preset switch
        {
            0 => MarkerBrushConfig.Smooth,
            1 => MarkerBrushConfig.Balanced,
            _ => MarkerBrushConfig.Sharp
        };
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, config);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);
        long timestamp = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        var pointer = new Point(10, 40);
        renderer.OnDown(BrushInputSample.CreatePointer(pointer, timestamp));

        for (int i = 1; i <= 12; i++)
        {
            timestamp += step;
            pointer = new Point(10 + (i * 24), 40 + (i * 0.75));
            renderer.OnMove(BrushInputSample.CreatePointer(pointer, timestamp));
        }

        var acceptedTip = renderer.GetLastStrokePoints()!.Last().Position;
        (pointer - acceptedTip).Length.Should().BeLessThanOrEqualTo(2.0);
    }

    [Theory]
    [MemberData(nameof(ClassroomSpeedScenarios))]
    public void ClassroomSpeedPointerMove_ShouldKeepAcceptedTipWithinThreeDip(
        int preset,
        int sampleRateHz,
        double dipPerFrame)
    {
        var config = preset switch
        {
            0 => MarkerBrushConfig.Smooth,
            1 => MarkerBrushConfig.Balanced,
            _ => MarkerBrushConfig.Sharp
        };
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, config);
        renderer.Initialize(Colors.Red, baseSize: 12, opacity: 255);
        long timestamp = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / sampleRateHz);
        var pointer = new Point(10, 40);
        renderer.OnDown(BrushInputSample.CreatePointer(pointer, timestamp));

        for (int i = 1; i <= 24; i++)
        {
            timestamp += step;
            pointer = new Point(10 + (i * dipPerFrame), 40 + (i * 0.5));
            renderer.OnMove(BrushInputSample.CreatePointer(pointer, timestamp));
        }

        var acceptedTip = renderer.GetLastStrokePoints()!.Last().Position;
        (pointer - acceptedTip).Length.Should().BeLessThanOrEqualTo(3.0);
    }
}
