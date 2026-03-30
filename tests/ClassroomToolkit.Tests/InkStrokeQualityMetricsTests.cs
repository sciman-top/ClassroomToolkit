using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkStrokeQualityMetricsTests
{
    [Fact]
    public void Analyze_ShouldYieldReasonableCompositeScore_ForNoisyReplay()
    {
        var raw = BuildNoisyLine();
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.DarkBlue, baseSize: 12, opacity: 255);

        var output = Replay(renderer, raw).Select(p => p.Position).ToList();
        var widths = renderer.GetLastStrokePoints()!.Select(p => p.Width).ToList();
        var report = InkStrokeQualityMetrics.Analyze(raw, output, widths);

        report.CompositeScore.Should().BeGreaterThan(0.18);
        report.JitterStdDev.Should().BeLessThan(4.0);
    }

    [Fact]
    public void Analyze_ShouldReportHigherCornerScore_ForLStroke()
    {
        var raw = BuildCornerStroke();
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);
        var output = Replay(renderer, raw).Select(p => p.Position).ToList();

        var report = InkStrokeQualityMetrics.Analyze(raw, output, renderer.GetLastStrokePoints()!.Select(p => p.Width).ToList());
        report.CornerScore.Should().BeGreaterThan(0.2);
    }

    private static List<Point> BuildNoisyLine()
    {
        var points = new List<Point>();
        for (int i = 0; i < 42; i++)
        {
            var x = 16 + i * 4.9;
            var y = 24 + i * 0.7;
            var noise = System.Math.Sin(i * 0.82) * 2.1 + System.Math.Cos(i * 0.41) * 0.75;
            points.Add(new Point(x, y + noise));
        }
        return points;
    }

    private static List<Point> BuildCornerStroke()
    {
        var points = new List<Point>();
        for (int i = 0; i <= 16; i++)
        {
            points.Add(new Point(18 + (i * 4.5), 26));
        }
        for (int i = 1; i <= 14; i++)
        {
            points.Add(new Point(90, 26 + (i * 4.2)));
        }
        return points;
    }

    private static List<StrokePointData> Replay(IBrushRenderer renderer, IReadOnlyList<Point> input)
    {
        long now = Stopwatch.GetTimestamp();
        long step = System.Math.Max(1, Stopwatch.Frequency / 120);
        for (int i = 0; i < input.Count; i++)
        {
            var sample = BrushInputSample.CreateStylus(input[i], now, 0.72);
            if (i == 0)
            {
                renderer.OnDown(sample);
            }
            else if (i == input.Count - 1)
            {
                renderer.OnUp(sample);
            }
            else
            {
                renderer.OnMove(sample);
            }
            now += step;
        }

        return renderer.GetLastStrokePoints() ?? new List<StrokePointData>();
    }
}
