using System;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Paint.Brushes;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class BrushSpeedReferenceScalerTests
{
    [Theory]
    [InlineData(2400.0, 1.0)]
    [InlineData(1200.0, 1.0)]
    [InlineData(4800.0, 2.0)]
    [InlineData(9600.0, 2.2)]
    [InlineData(0.0, 1.0)]
    [InlineData(-100.0, 1.0)]
    public void ResolveScale_ShouldNormalizeByReferenceDiagonal(double diagonalDip, double expected)
    {
        BrushSpeedReferenceScaler.ResolveScale(diagonalDip).Should().BeApproximately(expected, 0.001);
    }

    [Fact]
    public void ResolveScale_ShouldReturnOne_ForNonFiniteDiagonal()
    {
        BrushSpeedReferenceScaler.ResolveScale(double.NaN).Should().Be(1.0);
        BrushSpeedReferenceScaler.ResolveScale(double.PositiveInfinity).Should().Be(1.0);
    }

    [Fact]
    public void Apply_ShouldScaleOnlyAbsoluteSpeedReferenceFamily()
    {
        var config = BrushPhysicsConfig.CreateCalligraphyClarity();
        double velocityThreshold = config.VelocityThreshold;
        double speedFloor = config.SpeedFloorPxPerMs;
        double minVelocityClamp = config.MinVelocityClamp;
        double lowPass = config.WidthLowPassSpeedReference;
        double position = config.PositionSmoothingSpeedReference;
        double sampling = config.AdaptiveSamplingSpeedReference;
        double widthSmoothing = config.WidthSmoothing;
        double pressureInfluence = config.RealPressureWidthInfluence;

        BrushSpeedReferenceScaler.ApplyToCalligraphyConfig(config, 1.8);

        config.VelocityThreshold.Should().BeApproximately(velocityThreshold * 1.8, 0.001);
        config.SpeedFloorPxPerMs.Should().BeApproximately(speedFloor * 1.8, 0.001);
        config.MinVelocityClamp.Should().BeApproximately(minVelocityClamp * 1.8, 0.001);
        config.WidthLowPassSpeedReference.Should().BeApproximately(lowPass * 1.8, 0.001);
        config.PositionSmoothingSpeedReference.Should().BeApproximately(position * 1.8, 0.001);
        config.AdaptiveSamplingSpeedReference.Should().BeApproximately(sampling * 1.8, 0.001);
        config.WidthSmoothing.Should().Be(widthSmoothing);
        config.RealPressureWidthInfluence.Should().Be(pressureInfluence);
    }

    [Fact]
    public void Apply_ShouldBeIdempotent_WhenReappliedAtUnitScale()
    {
        var scaled = BrushPhysicsConfig.CreateCalligraphyClarity();
        BrushSpeedReferenceScaler.ApplyToCalligraphyConfig(scaled, 2.0);
        BrushSpeedReferenceScaler.ApplyToCalligraphyConfig(scaled, 1.0);

        var reference = BrushPhysicsConfig.CreateCalligraphyClarity();
        BrushSpeedReferenceScaler.ApplyToCalligraphyConfig(reference, 2.0);

        scaled.VelocityThreshold.Should().Be(reference.VelocityThreshold);
        scaled.SpeedFloorPxPerMs.Should().Be(reference.SpeedFloorPxPerMs);
        scaled.MinVelocityClamp.Should().Be(reference.MinVelocityClamp);
        scaled.PositionSmoothingSpeedReference.Should().Be(reference.PositionSmoothingSpeedReference);
    }
}
