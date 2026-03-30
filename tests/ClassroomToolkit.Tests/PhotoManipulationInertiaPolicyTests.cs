using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PhotoManipulationInertiaPolicyTests
{
    [Fact]
    public void ResolveTranslationDeceleration_ShouldUseDefault_WhenNotCrossPage()
    {
        var value = PhotoManipulationInertiaPolicy.ResolveTranslationDeceleration(crossPageDisplayActive: false);

        value.Should().Be(PhotoPanInertiaDefaults.GestureTranslationDecelerationDipPerMs2);
    }

    [Fact]
    public void ResolveTranslationDeceleration_ShouldUseCrossPageValue_WhenCrossPageEnabled()
    {
        var value = PhotoManipulationInertiaPolicy.ResolveTranslationDeceleration(crossPageDisplayActive: true);

        value.Should().Be(PhotoPanInertiaDefaults.GestureCrossPageTranslationDecelerationDipPerMs2);
    }

    [Fact]
    public void ResolveTranslationDeceleration_ShouldUseProfileTuning_WhenProvided()
    {
        var tuning = new PhotoPanInertiaTuning(
            MouseDecelerationDipPerMs2: 0.0022,
            MouseStopSpeedDipPerMs: 0.012,
            MouseMinReleaseSpeedDipPerMs: 0.06,
            MouseMaxReleaseSpeedDipPerMs: 4.4,
            MouseMaxDurationMs: 1100,
            MouseMaxTranslationPerFrameDip: 150,
            GestureTranslationDecelerationDipPerMs2: 0.009,
            GestureCrossPageTranslationDecelerationDipPerMs2: 0.008);

        var value = PhotoManipulationInertiaPolicy.ResolveTranslationDeceleration(
            crossPageDisplayActive: true,
            tuning);

        value.Should().Be(0.008);
    }
}
