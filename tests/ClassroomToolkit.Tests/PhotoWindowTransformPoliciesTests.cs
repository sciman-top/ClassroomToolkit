using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Windows;
using WpfPoint = System.Windows.Point;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoLoadedBitmapTransformPathPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnStoredPath_WhenCrossPagePathDisabled()
    {
        var path = PhotoWindowTransformPolicies.ResolvePhotoLoadedBitmapTransformPath(
            useCrossPageUnifiedPath: false,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.TryStoredTransformThenFit);
    }

    [Fact]
    public void Resolve_ShouldReturnUnifiedPath_WhenCrossPageEnabledAndUnifiedReady()
    {
        var path = PhotoWindowTransformPolicies.ResolvePhotoLoadedBitmapTransformPath(
            useCrossPageUnifiedPath: true,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.ApplyUnifiedTransform);
    }

    [Fact]
    public void Resolve_ShouldReturnFitPath_WhenCrossPageEnabledAndUnifiedNotReady()
    {
        var path = PhotoWindowTransformPolicies.ResolvePhotoLoadedBitmapTransformPath(
            useCrossPageUnifiedPath: true,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: false);

        path.Should().Be(PhotoLoadedBitmapTransformPath.FitToViewport);
    }

    [Fact]
    public void Resolve_ShouldReturnFitPath_WhenMemoryDisabledAndCrossPagePathDisabled()
    {
        var path = PhotoWindowTransformPolicies.ResolvePhotoLoadedBitmapTransformPath(
            useCrossPageUnifiedPath: false,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.FitToViewport);
    }

    [Fact]
    public void Resolve_ShouldReturnFitPath_WhenMemoryDisabledEvenWithCrossPageUnifiedReady()
    {
        var path = PhotoWindowTransformPolicies.ResolvePhotoLoadedBitmapTransformPath(
            useCrossPageUnifiedPath: true,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: true);

        path.Should().Be(PhotoLoadedBitmapTransformPath.FitToViewport);
    }
}

public sealed class PhotoManipulationAdmissionPolicyTests
{
    [Theory]
    [InlineData(false, false, PaintToolMode.Cursor, false, false, 1, false, false)]
    [InlineData(true, true, PaintToolMode.Cursor, false, false, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Brush, false, false, 2, true, true)]
    [InlineData(true, false, PaintToolMode.Cursor, true, false, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, true, 2, false, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, 1, true, true)]
    [InlineData(true, false, PaintToolMode.Cursor, false, false, 2, true, true)]
    public void Resolve_ShouldMatchExpected(
        bool photoModeActive,
        bool boardActive,
        PaintToolMode mode,
        bool inkOperationActive,
        bool photoPanning,
        int activeTouchCount,
        bool expectedShouldHandle,
        bool expectedShouldMarkHandled)
    {
        var plan = PhotoWindowTransformPolicies.ResolvePhotoManipulationAdmission(
            photoModeActive,
            boardActive,
            mode,
            inkOperationActive,
            photoPanning,
            activeTouchCount);

        plan.ShouldHandle.Should().Be(expectedShouldHandle);
        plan.ShouldMarkHandled.Should().Be(expectedShouldMarkHandled);
    }
}

public sealed class PhotoManipulationDeltaExecutionPolicyTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(0.05, 0.05, false)]
    [InlineData(0.2, 0.0, true)]
    [InlineData(0.0, -0.2, true)]
    public void Resolve_ShouldDetermineTranslationExecution(
        double dx,
        double dy,
        bool expectedShouldApplyTranslation)
    {
        var plan = PhotoWindowTransformPolicies.ResolvePhotoManipulationDeltaExecution(
            new Vector(dx, dy),
            translationEpsilonDip: 0.1,
            crossPageDisplayActive: false);

        plan.ShouldApplyTranslation.Should().Be(expectedShouldApplyTranslation);
        plan.ShouldLogPanTelemetry.Should().Be(expectedShouldApplyTranslation);
        plan.ShouldRequestCrossPageUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestCrossPageUpdate_WhenCrossPageDisplayActive()
    {
        var plan = PhotoWindowTransformPolicies.ResolvePhotoManipulationDeltaExecution(
            new Vector(0, 0),
            translationEpsilonDip: 0.1,
            crossPageDisplayActive: true);

        plan.ShouldRequestCrossPageUpdate.Should().BeTrue();
    }
}

public sealed class PhotoManipulationInertiaPolicyTests
{
    [Fact]
    public void ResolveTranslationDeceleration_ShouldUseDefault_WhenNotCrossPage()
    {
        var value = PhotoWindowTransformPolicies.ResolveTranslationDeceleration(crossPageDisplayActive: false);

        value.Should().Be(PhotoPanInertiaDefaults.GestureTranslationDecelerationDipPerMs2);
    }

    [Fact]
    public void ResolveTranslationDeceleration_ShouldUseCrossPageValue_WhenCrossPageEnabled()
    {
        var value = PhotoWindowTransformPolicies.ResolveTranslationDeceleration(crossPageDisplayActive: true);

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

        var value = PhotoWindowTransformPolicies.ResolveTranslationDeceleration(
            crossPageDisplayActive: true,
            tuning);

        value.Should().Be(0.008);
    }
}

public sealed class PhotoTransformMemoryTogglePolicyTests
{
    [Fact]
    public void ShouldResetUserDirtyState_ShouldReturnTrue_WhenMemoryDisabled()
    {
        PhotoWindowTransformPolicies.ShouldResetUserDirtyState(rememberPhotoTransform: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldResetUserDirtyState_ShouldReturnFalse_WhenMemoryEnabled()
    {
        PhotoWindowTransformPolicies.ShouldResetUserDirtyState(rememberPhotoTransform: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldResetUnifiedTransformState_ShouldReturnTrue_WhenMemoryDisabled()
    {
        PhotoWindowTransformPolicies.ShouldResetUnifiedTransformState(rememberPhotoTransform: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldResetUnifiedTransformState_ShouldReturnFalse_WhenMemoryEnabled()
    {
        PhotoWindowTransformPolicies.ShouldResetUnifiedTransformState(rememberPhotoTransform: true)
            .Should()
            .BeFalse();
    }
}

public sealed class PhotoUnifiedTransformApplyPolicyTests
{
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(false, false, true, false)]
    [InlineData(false, true, false, false)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ShouldApplyRuntimeTransform_ShouldMatchExpected(
        bool rememberPhotoTransform,
        bool photoInkModeActive,
        bool crossPageDisplayActive,
        bool expected)
    {
        PhotoWindowTransformPolicies.ShouldApplyRuntimeTransform(
                rememberPhotoTransform,
                photoInkModeActive,
                crossPageDisplayActive)
            .Should()
            .Be(expected);
    }
}

public sealed class PhotoZoomAnchorPolicyTests
{
    [Theory]
    [InlineData(1920, 1080, 960, 540)]
    [InlineData(1366, 768, 683, 384)]
    [InlineData(1, 1, 0.5, 0.5)]
    public void ResolveViewportCenter_ShouldReturnViewportMidpoint(
        double width,
        double height,
        double expectedX,
        double expectedY)
    {
        PhotoWindowTransformPolicies.ResolveViewportCenter(width, height)
            .Should()
            .Be(new WpfPoint(expectedX, expectedY));
    }

    [Theory]
    [InlineData(0, 1080)]
    [InlineData(1920, 0)]
    [InlineData(-1, 1080)]
    public void ResolveViewportCenter_ShouldReturnDefault_WhenViewportIsInvalid(
        double width,
        double height)
    {
        PhotoWindowTransformPolicies.ResolveViewportCenter(width, height)
            .Should()
            .Be(default(WpfPoint));
    }
}
