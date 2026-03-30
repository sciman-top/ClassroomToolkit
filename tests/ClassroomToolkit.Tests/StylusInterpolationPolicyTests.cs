using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusInterpolationPolicyTests
{
    [Fact]
    public void ResolveInterpolationStepDip_ShouldClampToConfiguredRange()
    {
        var step = StylusInterpolationPolicy.ResolveInterpolationStepDip(
            brushSize: 100,
            distance: 800,
            totalTicks: 10,
            stopwatchFrequency: 1000);

        step.Should().Be(StylusInterpolationDefaults.InterpolationStepMaxDip);
    }

    [Fact]
    public void ShouldInterpolate_ShouldUseConfiguredDistanceMultiplier()
    {
        StylusInterpolationPolicy.ShouldInterpolate(distance: 10, interpolationStepDip: 10).Should().BeFalse();
        StylusInterpolationPolicy.ShouldInterpolate(distance: 15, interpolationStepDip: 10).Should().BeTrue();
    }

    [Fact]
    public void ResolveMaxSegments_ShouldRespectSpeedBandAndSlowFrameBonus()
    {
        StylusInterpolationPolicy.ResolveMaxSegments(speedDipPerMs: 3.5, dtMs: 2)
            .Should().Be(StylusInterpolationDefaults.FastSpeedMaxSegments);

        StylusInterpolationPolicy.ResolveMaxSegments(speedDipPerMs: 1.0, dtMs: 12)
            .Should().Be(8);
    }
}
