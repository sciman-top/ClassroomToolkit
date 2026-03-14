using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class StylusInterpolationDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchStabilizedValues()
    {
        StylusInterpolationDefaults.MinDtMsForSpeed.Should().Be(0.2);
        StylusInterpolationDefaults.SpeedNormBase.Should().Be(0.9);
        StylusInterpolationDefaults.SpeedNormRange.Should().Be(2.4);
        StylusInterpolationDefaults.StepScaleBase.Should().Be(0.9);
        StylusInterpolationDefaults.StepScaleSpeedMultiplier.Should().Be(0.55);
        StylusInterpolationDefaults.InterpolationStepMinDip.Should().Be(3.0);
        StylusInterpolationDefaults.InterpolationStepMaxDip.Should().Be(12.0);
        StylusInterpolationDefaults.DistanceTriggerMultiplier.Should().Be(1.4);
        StylusInterpolationDefaults.FastSpeedThreshold.Should().Be(3.2);
        StylusInterpolationDefaults.MediumSpeedThreshold.Should().Be(2.2);
        StylusInterpolationDefaults.SlowSpeedThreshold.Should().Be(1.4);
        StylusInterpolationDefaults.FastSpeedMaxSegments.Should().Be(4);
        StylusInterpolationDefaults.MediumSpeedMaxSegments.Should().Be(5);
        StylusInterpolationDefaults.SlowSpeedMaxSegments.Should().Be(6);
        StylusInterpolationDefaults.DefaultMaxSegments.Should().Be(7);
        StylusInterpolationDefaults.MinSegmentCount.Should().Be(2);
        StylusInterpolationDefaults.SlowFrameDtThresholdMs.Should().Be(10.0);
        StylusInterpolationDefaults.SlowFrameMaxSegmentsBonus.Should().Be(1);
        StylusInterpolationDefaults.MaxSegmentsCap.Should().Be(8);
        StylusInterpolationDefaults.SegmentProgressUpperBound.Should().Be(1.0);
        StylusInterpolationDefaults.MinTimestampStepTicks.Should().Be(1);
    }
}
