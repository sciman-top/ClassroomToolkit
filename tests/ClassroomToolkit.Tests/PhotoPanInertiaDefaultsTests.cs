using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanInertiaDefaultsTests
{
    [Fact]
    public void Defaults_ShouldMatchTunedValues()
    {
        PhotoPanInertiaDefaults.MouseTickIntervalMs.Should().Be(16);
        PhotoPanInertiaDefaults.MouseDecelerationDipPerMs2.Should().Be(0.0022);
        PhotoPanInertiaDefaults.MouseStopSpeedDipPerMs.Should().Be(0.012);
        PhotoPanInertiaDefaults.MouseFrameElapsedMinMs.Should().Be(1.0);
        PhotoPanInertiaDefaults.MouseFrameElapsedMaxMs.Should().Be(34.0);
        PhotoPanInertiaDefaults.MouseMaxDurationMs.Should().Be(1100.0);
        PhotoPanInertiaDefaults.MouseMaxTranslationPerFrameDip.Should().Be(150.0);
        PhotoPanInertiaDefaults.MouseMinReleaseSpeedDipPerMs.Should().Be(0.06);
        PhotoPanInertiaDefaults.MouseMaxReleaseSpeedDipPerMs.Should().Be(4.4);
        PhotoPanInertiaDefaults.MouseMinVelocitySampleDistanceDip.Should().Be(0.9);
        PhotoPanInertiaDefaults.MouseMaxVelocitySampleAgeMs.Should().Be(140);
        PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs.Should().Be(6);
        PhotoPanInertiaDefaults.MouseVelocitySampleWindowMs.Should().Be(120);
        PhotoPanInertiaDefaults.MouseVelocitySampleHistoryMaxAgeMs.Should().Be(220);
        PhotoPanInertiaDefaults.MouseVelocitySampleCapacity.Should().Be(12);
        PhotoPanInertiaDefaults.MouseVelocityRecentWeightGain.Should().Be(0.75);
        PhotoPanInertiaDefaults.GestureTranslationDecelerationDipPerMs2.Should().Be(0.0034);
        PhotoPanInertiaDefaults.GestureCrossPageTranslationDecelerationDipPerMs2.Should().Be(0.0029);
    }
}

