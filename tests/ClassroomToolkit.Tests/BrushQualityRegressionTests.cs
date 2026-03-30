using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ClassroomToolkit.App.Paint.Brushes;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class BrushQualityRegressionTests
{
    [Fact]
    public void MarkerRenderer_ShouldReduceLateralJitter_OnNoisyLine()
    {
        var input = BuildNoisyLine();
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Blue, baseSize: 12, opacity: 255);

        var output = Replay(renderer, input, hasPressure: false);

        var baseline = (input[0], input[^1]);
        var inputJitter = ComputeDistanceStdDev(input, baseline);
        var outputJitter = ComputeDistanceStdDev(output.Select(p => p.Position).ToList(), baseline);

        outputJitter.Should().BeLessThan(inputJitter * 0.91);
    }

    [Fact]
    public void CalligraphyRenderer_ShouldReduceLateralJitter_OnNoisyLine()
    {
        var input = BuildNoisyLine();
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        var output = Replay(renderer, input, hasPressure: false);

        var baseline = (input[0], input[^1]);
        var inputJitter = ComputeDistanceStdDev(input, baseline);
        var outputJitter = ComputeDistanceStdDev(output.Select(p => p.Position).ToList(), baseline);

        outputJitter.Should().BeLessThan(inputJitter * 0.91);
    }

    [Fact]
    public void CalligraphyRenderer_ShouldPreserveCornerShape_OnLStroke()
    {
        var input = BuildCornerStroke();
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        var output = Replay(renderer, input, hasPressure: false);
        output.Count.Should().BeGreaterThan(6);

        var corner = new Point(82, 20);
        var closestIndex = 0;
        var closestDistance = double.MaxValue;
        for (int i = 0; i < output.Count; i++)
        {
            var distance = (output[i].Position - corner).Length;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        closestDistance.Should().BeLessThan(9.0);
        closestIndex.Should().BeGreaterThan(2);
        closestIndex.Should().BeLessThan(output.Count - 3);

        var prev = output[closestIndex - 2].Position;
        var curr = output[closestIndex].Position;
        var next = output[closestIndex + 2].Position;
        var angle = Math.Abs(Vector.AngleBetween(curr - prev, next - curr));
        angle.Should().BeGreaterThan(45.0);
    }

    [Fact]
    public void MarkerRenderer_ShouldKeepContinuity_OnSparseFastTrace()
    {
        var input = BuildSparseFastTrace();
        var renderer = new MarkerBrushRenderer(MarkerRenderMode.Ribbon, MarkerBrushConfig.Balanced);
        renderer.Initialize(Colors.Blue, baseSize: 12, opacity: 255);

        var output = Replay(renderer, input, hasPressure: true, hz: 60);
        output.Count.Should().BeGreaterThanOrEqualTo(input.Count - 2);
        var inputMaxSegment = ComputeMaxSegmentLength(input);
        var outputMaxSegment = ComputeMaxSegmentLength(output.Select(p => p.Position).ToList());
        outputMaxSegment.Should().BeLessThan(inputMaxSegment * 1.25);
    }

    [Fact]
    public void CalligraphyRenderer_ShouldKeepContinuity_OnSparseFastTrace()
    {
        var input = BuildSparseFastTrace();
        var renderer = new VariableWidthBrushRenderer(BrushPhysicsConfig.CreateCalligraphyBalanced());
        renderer.Initialize(Colors.Black, baseSize: 12, opacity: 255);

        var output = Replay(renderer, input, hasPressure: true, hz: 60);
        output.Count.Should().BeGreaterThanOrEqualTo(input.Count - 3);
        var inputMaxSegment = ComputeMaxSegmentLength(input);
        var outputMaxSegment = ComputeMaxSegmentLength(output.Select(p => p.Position).ToList());
        outputMaxSegment.Should().BeLessThan(inputMaxSegment * 1.2);
        (output[^1].Position - input[^1]).Length.Should().BeLessThan(18.0);
    }

    private static List<Point> BuildNoisyLine()
    {
        var points = new List<Point>();
        for (int i = 0; i < 40; i++)
        {
            var x = 20 + i * 5.0;
            var y = 30 + i * 0.72;
            var noise = Math.Sin(i * 0.95) * 1.9 + Math.Cos(i * 0.37) * 0.8;
            points.Add(new Point(x, y + noise));
        }
        return points;
    }

    private static List<Point> BuildCornerStroke()
    {
        var points = new List<Point>();
        for (int i = 0; i <= 14; i++)
        {
            points.Add(new Point(12 + (i * 5), 20));
        }
        for (int i = 1; i <= 14; i++)
        {
            points.Add(new Point(82, 20 + (i * 4.6)));
        }
        return points;
    }

    private static List<Point> BuildSparseFastTrace()
    {
        var points = new List<Point>();
        for (int i = 0; i < 18; i++)
        {
            double x = 20 + (i * 56.0);
            double y = 100 + (Math.Sin(i * 0.72) * 45.0) + (Math.Cos(i * 0.28) * 16.0);
            points.Add(new Point(x, y));
        }
        return points;
    }

    private static List<StrokePointData> Replay(IBrushRenderer renderer, IReadOnlyList<Point> input, bool hasPressure, int hz = 120)
    {
        input.Count.Should().BeGreaterThanOrEqualTo(2);
        long now = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / Math.Max(1, hz));

        for (int i = 0; i < input.Count; i++)
        {
            var sample = hasPressure
                ? BrushInputSample.CreateStylus(input[i], now, 0.75)
                : BrushInputSample.CreatePointer(input[i], now);

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

    private static double ComputeMaxSegmentLength(IReadOnlyList<Point> points)
    {
        if (points.Count < 2)
        {
            return 0.0;
        }

        double max = 0.0;
        for (int i = 1; i < points.Count; i++)
        {
            max = Math.Max(max, (points[i] - points[i - 1]).Length);
        }
        return max;
    }

    private static double ComputeDistanceStdDev(IReadOnlyList<Point> points, (Point Start, Point End) baseline)
    {
        var distances = points.Select(point => DistanceToLine(point, baseline.Start, baseline.End)).ToList();
        if (distances.Count == 0)
        {
            return 0.0;
        }
        var avg = distances.Average();
        var variance = distances.Select(value => (value - avg) * (value - avg)).Average();
        return Math.Sqrt(variance);
    }

    private static double DistanceToLine(Point p, Point a, Point b)
    {
        var ab = b - a;
        var abLenSq = (ab.X * ab.X) + (ab.Y * ab.Y);
        if (abLenSq < 0.000001)
        {
            return (p - a).Length;
        }
        var ap = p - a;
        var t = ((ap.X * ab.X) + (ap.Y * ab.Y)) / abLenSq;
        var projection = new Point(a.X + ab.X * t, a.Y + ab.Y * t);
        return (p - projection).Length;
    }
}
