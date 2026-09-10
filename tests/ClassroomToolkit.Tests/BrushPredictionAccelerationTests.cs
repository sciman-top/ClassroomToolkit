using System;
using System.Diagnostics;
using System.Windows;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

/// <summary>
/// 预测段二阶外推（加速度项）契约：只作用于预览 ghost，不进入已提交几何。
/// </summary>
public sealed class BrushPredictionAccelerationTests
{
    private static readonly long StepTicks = Math.Max(1, Stopwatch.Frequency / 120);

    [Fact]
    public void Acceleration_ShouldStayNearZero_ForConstantVelocity()
    {
        var acceleration = new Vector();
        var previous = new BrushInputSample(new Point(0, 0), Timestamp(0), 0.5, false);
        var velocity = new Vector();

        // 起步瞬态（速度从 0 上升到匀速）会先产生被钳制的加速度，
        // EMA 以 0.72/步衰减，需要足够步数后回到近零。
        for (int i = 1; i <= 24; i++)
        {
            var current = new BrushInputSample(new Point(i * 8.0, 0), Timestamp(i), 0.5, false);
            var previousVelocity = velocity;
            velocity = BrushPredictionVelocityPolicy.Resolve(velocity, previous, current);
            acceleration = BrushPredictionVelocityPolicy.ResolveAcceleration(
                acceleration, previous, current, previousVelocity, velocity);
            previous = current;
        }

        acceleration.Length.Should().BeLessThan(400.0,
            "constant-velocity input must not accumulate phantom acceleration");
    }

    [Fact]
    public void Acceleration_ShouldAlignWithSpeedUpDirection()
    {
        var acceleration = new Vector();
        var previous = new BrushInputSample(new Point(0, 0), Timestamp(0), 0.5, false);
        var velocity = new Vector();

        for (int i = 1; i <= 12; i++)
        {
            var current = new BrushInputSample(new Point(i * i * 0.9, 0), Timestamp(i), 0.5, false);
            var previousVelocity = velocity;
            velocity = BrushPredictionVelocityPolicy.Resolve(velocity, previous, current);
            acceleration = BrushPredictionVelocityPolicy.ResolveAcceleration(
                acceleration, previous, current, previousVelocity, velocity);
            previous = current;
        }

        acceleration.X.Should().BeGreaterThan(0.0);
        acceleration.Y.Should().Be(0.0);
    }

    [Fact]
    public void Acceleration_ShouldBeClamped_ForTeleportLikeVelocityJump()
    {
        var baseSample = new BrushInputSample(new Point(0, 0), Timestamp(0), 0.5, false);
        var nextSample = new BrushInputSample(new Point(0, 0), Timestamp(1), 0.5, false);
        var hugeVelocity = new Vector(1e7, 0);

        var acceleration = BrushPredictionVelocityPolicy.ResolveAcceleration(
            new Vector(), baseSample, nextSample, new Vector(), hugeVelocity);

        acceleration.Length.Should().BeLessThanOrEqualTo(
            BrushPredictionPreviewDefaults.MaxAccelerationDipPerSecSq + 0.001);
    }

    [Fact]
    public void Acceleration_ShouldKeepPreviousValue_WhenSamplesAreTooClose()
    {
        var first = new BrushInputSample(new Point(0, 0), Timestamp(0), 0.5, false);
        var second = new BrushInputSample(new Point(100, 0), Timestamp(0), 0.5, false);
        var kept = new Vector(123, 45);

        var result = BrushPredictionVelocityPolicy.ResolveAcceleration(
            kept, first, second, new Vector(), new Vector(10, 0));

        result.Should().Be(kept);
    }

    private static long Timestamp(int step)
    {
        return step * StepTicks;
    }
}
