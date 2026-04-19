using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanReleaseTuningPolicyTests
{
    [Fact]
    public void ResolveTouch_ShouldLowerThreshold_AndIncreaseTravelComparedToMouse()
    {
        var baseTuning = PhotoPanInertiaTuning.Default;

        var mouse = PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, baseTuning);
        var touch = PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Touch, baseTuning);

        touch.MinReleaseSpeedDipPerMs.Should().BeLessThan(mouse.MinReleaseSpeedDipPerMs);
        touch.MaxReleaseSpeedDipPerMs.Should().BeGreaterThan(mouse.MaxReleaseSpeedDipPerMs);
        touch.DecelerationDipPerMs2.Should().BeLessThan(mouse.DecelerationDipPerMs2);
        touch.MaxDurationMs.Should().BeGreaterThan(mouse.MaxDurationMs);
        touch.MaxTranslationPerFrameDip.Should().BeGreaterThan(mouse.MaxTranslationPerFrameDip);
    }

    [Fact]
    public void ResolveTouch_ShouldUseWiderSamplingTolerance_ForLowRateTouchHardware()
    {
        var baseTuning = PhotoPanInertiaTuning.Default;

        var mouse = PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Mouse, baseTuning);
        var touch = PhotoPanReleaseTuningPolicy.Resolve(PhotoPanPointerKind.Touch, baseTuning);

        touch.MaxVelocitySampleAgeMs.Should().BeGreaterThan(mouse.MaxVelocitySampleAgeMs);
        touch.VelocitySampleWindowMs.Should().BeGreaterThan(mouse.VelocitySampleWindowMs);
        touch.MinVelocitySampleDistanceDip.Should().BeLessThan(mouse.MinVelocitySampleDistanceDip);
        touch.VelocityRecentWeightGain.Should().BeGreaterThan(mouse.VelocityRecentWeightGain);
    }
}
