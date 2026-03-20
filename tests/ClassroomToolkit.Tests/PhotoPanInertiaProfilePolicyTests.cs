using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoPanInertiaProfilePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnDefaultTuning_ForStandardProfile()
    {
        var tuning = PhotoPanInertiaProfilePolicy.Resolve(PhotoInertiaProfileDefaults.Standard);

        tuning.Should().Be(PhotoPanInertiaTuning.Default);
    }

    [Fact]
    public void Resolve_ShouldReturnSensitiveTuning()
    {
        var tuning = PhotoPanInertiaProfilePolicy.Resolve(PhotoInertiaProfileDefaults.Sensitive);

        tuning.MouseDecelerationDipPerMs2.Should().Be(0.0026);
        tuning.MouseMaxReleaseSpeedDipPerMs.Should().Be(5.2);
        tuning.MouseMaxDurationMs.Should().Be(980.0);
    }

    [Fact]
    public void Resolve_ShouldReturnHeavyTuning()
    {
        var tuning = PhotoPanInertiaProfilePolicy.Resolve(PhotoInertiaProfileDefaults.Heavy);

        tuning.MouseDecelerationDipPerMs2.Should().Be(0.0016);
        tuning.MouseStopSpeedDipPerMs.Should().Be(0.009);
        tuning.MouseMaxDurationMs.Should().Be(1500.0);
    }
}
