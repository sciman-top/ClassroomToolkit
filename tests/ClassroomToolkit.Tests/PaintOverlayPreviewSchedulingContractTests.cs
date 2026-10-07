using AwesomeAssertions;
using System.Diagnostics;
using System.Windows;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayPreviewSchedulingContractTests
{
    [Fact]
    public void PredictionVelocity_ShouldAdvanceForDistinctMonotonicSamples()
    {
        long start = Stopwatch.GetTimestamp();
        long step = Math.Max(1, Stopwatch.Frequency / 120);
        var previous = BrushInputSample.CreatePointer(new Point(10, 20), start);
        var current = BrushInputSample.CreatePointer(new Point(30, 24), start + step);

        var velocity = PresetSchemePolicies.ResolveBrushPredictionVelocity(new Vector(), previous, current);

        velocity.X.Should().BeGreaterThan(0);
        velocity.Y.Should().BeGreaterThan(0);
    }

    [Fact]
    public void PredictionVelocity_ShouldIgnoreSamplesWithoutUsableTimeDelta()
    {
        long timestamp = Stopwatch.GetTimestamp();
        var previous = BrushInputSample.CreatePointer(new Point(10, 20), timestamp);
        var current = BrushInputSample.CreatePointer(new Point(30, 24), timestamp);
        var existing = new Vector(12, 4);

        PresetSchemePolicies.ResolveBrushPredictionVelocity(existing, previous, current).Should().Be(existing);
    }
}
