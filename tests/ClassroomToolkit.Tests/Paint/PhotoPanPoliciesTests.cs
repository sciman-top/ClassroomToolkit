using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Windows;
using System.Windows.Input;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class PhotoEnterTransformInitPolicyTests
{
    [Fact]
    public void Resolve_ShouldApplyUnifiedAndMarkDirty_WhenCrossPageAndUnifiedReady()
    {
        var plan = PhotoPanPolicies.ResolvePhotoEnterTransformInit(
            crossPageDisplayEnabled: true,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: true,
            hadUserTransformDirty: false);

        plan.ShouldApplyUnifiedTransform.Should().BeTrue();
        plan.ShouldMarkUserDirtyAfterUnifiedApply.Should().BeTrue();
        plan.ShouldMarkUnifiedTransformReady.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeFalse();
        plan.ShouldResetIdentity.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldResetIdentity_WhenCrossPageAndMemoryDisabled()
    {
        var plan = PhotoPanPolicies.ResolvePhotoEnterTransformInit(
            crossPageDisplayEnabled: true,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: false,
            hadUserTransformDirty: true);

        plan.ShouldApplyUnifiedTransform.Should().BeFalse();
        plan.ShouldMarkUserDirtyAfterUnifiedApply.Should().BeFalse();
        plan.ShouldMarkUnifiedTransformReady.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeFalse();
        plan.ShouldResetIdentity.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldTryStoredTransform_WhenSinglePageAndRememberEnabled()
    {
        var plan = PhotoPanPolicies.ResolvePhotoEnterTransformInit(
            crossPageDisplayEnabled: false,
            rememberPhotoTransform: true,
            photoUnifiedTransformReady: false,
            hadUserTransformDirty: false);

        plan.ShouldApplyUnifiedTransform.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeTrue();
        plan.ShouldResetIdentity.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldResetIdentity_WhenNoOtherPlanMatches()
    {
        var plan = PhotoPanPolicies.ResolvePhotoEnterTransformInit(
            crossPageDisplayEnabled: false,
            rememberPhotoTransform: false,
            photoUnifiedTransformReady: false,
            hadUserTransformDirty: false);

        plan.ShouldApplyUnifiedTransform.Should().BeFalse();
        plan.ShouldTryStoredTransform.Should().BeFalse();
        plan.ShouldResetIdentity.Should().BeTrue();
    }
}

public sealed class PhotoHorizontalPanRangePolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowEdgePanning_WhenPageNarrowerThanViewport()
    {
        var range = PhotoPanPolicies.ResolvePhotoHorizontalPanRange(
            viewportWidth: 1920,
            scaledWidth: 1200,
            includeSlack: false);

        range.MinX.Should().Be(0);
        range.MaxX.Should().Be(720);
    }

    [Fact]
    public void Resolve_ShouldUseSlack_WhenEnabled()
    {
        var range = PhotoPanPolicies.ResolvePhotoHorizontalPanRange(
            viewportWidth: 1000,
            scaledWidth: 600,
            includeSlack: true);

        range.MinX.Should().Be(-60);
        range.MaxX.Should().Be(460);
    }
}

public sealed class PhotoPanDragActivationPolicyTests
{
    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnFalse_WhenCrossPageInactive()
    {
        PhotoPanPolicies.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: false,
            deltaYDip: 20).Should().BeFalse();
    }

    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnFalse_WhenDeltaWithinThreshold()
    {
        PhotoPanPolicies.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: true,
            deltaYDip: PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip).Should().BeFalse();
    }

    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnTrue_WhenDeltaExceedsThreshold()
    {
        PhotoPanPolicies.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: true,
            deltaYDip: PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip + 0.1).Should().BeTrue();
    }

    [Fact]
    public void ShouldActivateCrossPageDrag_ShouldReturnTrue_WhenNegativeDeltaExceedsThreshold()
    {
        PhotoPanPolicies.ShouldActivateCrossPageDrag(
            crossPageDisplayActive: true,
            deltaYDip: -(PhotoPanDragActivationDefaults.CrossPageDragDeltaYThresholdDip + 0.1)).Should().BeTrue();
    }
}

public sealed class PhotoPanInertiaMotionPolicyTests
{
    [Fact]
    public void TryResolveReleaseVelocity_WithVelocitySampleWindow_ShouldIgnoreOldBurstSegments()
    {
        var samples = new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), TimestampTicks: 100),
            new PhotoPanVelocitySample(new Point(120, 0), TimestampTicks: 120),
            new PhotoPanVelocitySample(new Point(121, 0), TimestampTicks: 300)
        };

        var resolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 300,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out _);

        resolved.Should().BeFalse();
    }

    [Fact]
    public void TryResolveReleaseVelocity_WithVelocitySampleWindow_ShouldBiasRecentSegments()
    {
        var samples = new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), TimestampTicks: 1000),
            new PhotoPanVelocitySample(new Point(24, 0), TimestampTicks: 1040),
            new PhotoPanVelocitySample(new Point(48, 0), TimestampTicks: 1080),
            new PhotoPanVelocitySample(new Point(48, 36), TimestampTicks: 1100)
        };

        var resolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 1100,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.Y.Should().BeGreaterThan(velocityDipPerMs.X);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldReturnFalse_WhenSampleIsStale()
    {
        var resolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            previousPosition: new Point(10, 10),
            previousTimestampTicks: 100,
            lastPosition: new Point(80, 10),
            lastTimestampTicks: 120,
            releaseTimestampTicks: 301,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out _);

        resolved.Should().BeFalse();
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldClampSpeed_WhenTooFast()
    {
        var resolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            previousPosition: new Point(0, 0),
            previousTimestampTicks: 100,
            lastPosition: new Point(260, 0),
            lastTimestampTicks: 120,
            releaseTimestampTicks: 122,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.Length.Should().BeApproximately(PhotoPanInertiaDefaults.MouseMaxReleaseSpeedDipPerMs, 0.001);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldRespectTouchThreshold()
    {
        var tuning = PhotoPanPolicies.ResolveReleaseTuning(
            PhotoPanPointerKind.Touch,
            PhotoPanInertiaTuning.Default);

        var resolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            new[]
            {
                new PhotoPanVelocitySample(new Point(0, 0), 1000),
                new PhotoPanVelocitySample(new Point(1.5, 0), 1030),
                new PhotoPanVelocitySample(new Point(4.5, 0), 1060)
            },
            releaseTimestampTicks: 1060,
            stopwatchFrequency: 1000,
            tuning,
            out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.X.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldAllowTouchRelease_WhenLastSampleIsSlightlyOlder()
    {
        var mouseTuning = PhotoPanPolicies.ResolveReleaseTuning(
            PhotoPanPointerKind.Mouse,
            PhotoPanInertiaTuning.Default);
        var touchTuning = PhotoPanPolicies.ResolveReleaseTuning(
            PhotoPanPointerKind.Touch,
            PhotoPanInertiaTuning.Default);
        var samples = new[]
        {
            new PhotoPanVelocitySample(new Point(0, 0), 1000),
            new PhotoPanVelocitySample(new Point(24, 0), 1040),
            new PhotoPanVelocitySample(new Point(48, 0), 1080)
        };

        var mouseResolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 1260,
            stopwatchFrequency: 1000,
            mouseTuning,
            out _);
        var touchResolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            samples,
            releaseTimestampTicks: 1260,
            stopwatchFrequency: 1000,
            touchTuning,
            out var touchVelocityDipPerMs);

        mouseResolved.Should().BeFalse();
        touchResolved.Should().BeTrue();
        touchVelocityDipPerMs.X.Should().BeGreaterThan(0);
    }

    [Fact]
    public void TryResolveReleaseVelocity_ShouldFallbackToMinInterval_WhenSampleIntervalTooSmall()
    {
        var resolved = PhotoPanPolicies.TryResolveReleaseVelocity(
            previousPosition: new Point(0, 0),
            previousTimestampTicks: 1000,
            lastPosition: new Point(1.2, 0),
            lastTimestampTicks: 1002,
            releaseTimestampTicks: 1002,
            stopwatchFrequency: 1000,
            velocityDipPerMs: out var velocityDipPerMs);

        resolved.Should().BeTrue();
        velocityDipPerMs.X.Should().BeApproximately(
            1.2 / PhotoPanInertiaDefaults.MouseMinVelocitySampleIntervalMs,
            0.001);
    }

    [Fact]
    public void ResolveTranslation_ShouldScaleWithElapsedMilliseconds()
    {
        var translation = PhotoPanPolicies.ResolveTranslation(
            new Vector(1.2, -0.5),
            elapsedMs: 25);

        translation.X.Should().BeApproximately(30, 0.001);
        translation.Y.Should().BeApproximately(-12.5, 0.001);
    }

    [Fact]
    public void ResolveTranslation_ShouldClampDistance_WhenFrameJumpOccurs()
    {
        var translation = PhotoPanPolicies.ResolveTranslation(
            new Vector(4.4, 0),
            elapsedMs: 120);

        translation.Length.Should().BeApproximately(
            PhotoPanInertiaDefaults.MouseMaxTranslationPerFrameDip,
            0.001);
    }

    [Fact]
    public void TryResolveInertiaStep_ShouldBlendCurrentAndNextVelocity_ForSmootherTravel()
    {
        var tuning = PhotoPanPolicies.ResolveReleaseTuning(
            PhotoPanPointerKind.Mouse,
            PhotoPanInertiaTuning.Default);
        var velocityDipPerMs = new Vector(1.2, 0);
        var elapsedMs = 16.0;

        var resolved = PhotoPanPolicies.TryResolveInertiaStep(
            velocityDipPerMs,
            elapsedMs,
            tuning,
            out var translation,
            out var nextVelocityDipPerMs);

        var expectedNextVelocity = PhotoPanPolicies.ResolveVelocityAfterDeceleration(
            velocityDipPerMs,
            elapsedMs,
            tuning);
        var expectedTranslation = PhotoPanPolicies.ResolveTranslation(
            (velocityDipPerMs + expectedNextVelocity) * 0.5,
            elapsedMs,
            tuning);

        resolved.Should().BeTrue();
        nextVelocityDipPerMs.X.Should().BeApproximately(expectedNextVelocity.X, 0.001);
        translation.X.Should().BeApproximately(expectedTranslation.X, 0.001);
    }

    [Fact]
    public void ResolveFrameElapsedMilliseconds_ShouldClampToConfiguredRange()
    {
        var low = PhotoPanPolicies.ResolveFrameElapsedMilliseconds(0.2);
        var high = PhotoPanPolicies.ResolveFrameElapsedMilliseconds(180);

        low.Should().Be(PhotoPanInertiaDefaults.MouseFrameElapsedMinMs);
        high.Should().Be(PhotoPanInertiaDefaults.MouseFrameElapsedMaxMs);
    }

    [Fact]
    public void ShouldStopByDuration_ShouldReturnTrue_WhenDurationExceeded()
    {
        var shouldStop = PhotoPanPolicies.ShouldStopByDuration(
            PhotoPanInertiaDefaults.MouseMaxDurationMs + 1);

        shouldStop.Should().BeTrue();
    }

    [Fact]
    public void ResolveVelocityAfterDeceleration_ShouldReturnZero_WhenBelowStopThreshold()
    {
        var next = PhotoPanPolicies.ResolveVelocityAfterDeceleration(
            new Vector(0.03, 0),
            elapsedMs: 20);

        next.LengthSquared.Should().Be(0);
    }

    [Fact]
    public void ResolveTranslation_ShouldHonorTuningMaxDistance()
    {
        var tuning = new PhotoPanInertiaTuning(
            MouseDecelerationDipPerMs2: 0.0022,
            MouseStopSpeedDipPerMs: 0.012,
            MouseMinReleaseSpeedDipPerMs: 0.06,
            MouseMaxReleaseSpeedDipPerMs: 4.4,
            MouseMaxDurationMs: 1100,
            MouseMaxTranslationPerFrameDip: 60,
            GestureTranslationDecelerationDipPerMs2: 0.0034,
            GestureCrossPageTranslationDecelerationDipPerMs2: 0.0029);
        var translation = PhotoPanPolicies.ResolveTranslation(
            new Vector(4.4, 0),
            elapsedMs: 50,
            tuning);

        translation.Length.Should().BeApproximately(60, 0.001);
    }

    [Fact]
    public void ResolveTranslation_ShouldHonorTouchReleaseTuningFrameClamp()
    {
        var tuning = PhotoPanPolicies.ResolveReleaseTuning(
            PhotoPanPointerKind.Touch,
            PhotoPanInertiaTuning.Default);

        var translation = PhotoPanPolicies.ResolveTranslation(
            new Vector(6.0, 0),
            elapsedMs: 80,
            tuning);

        translation.Length.Should().BeLessThanOrEqualTo(tuning.MaxTranslationPerFrameDip);
    }

    [Fact]
    public void ShouldStopByDuration_ShouldHonorTuningDuration()
    {
        var tuning = new PhotoPanInertiaTuning(
            MouseDecelerationDipPerMs2: 0.0022,
            MouseStopSpeedDipPerMs: 0.012,
            MouseMinReleaseSpeedDipPerMs: 0.06,
            MouseMaxReleaseSpeedDipPerMs: 4.4,
            MouseMaxDurationMs: 500,
            MouseMaxTranslationPerFrameDip: 150,
            GestureTranslationDecelerationDipPerMs2: 0.0034,
            GestureCrossPageTranslationDecelerationDipPerMs2: 0.0029);

        PhotoPanPolicies.ShouldStopByDuration(501, tuning).Should().BeTrue();
    }
}

public sealed class PhotoPanInertiaProfilePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnDefaultTuning_ForStandardProfile()
    {
        var tuning = PhotoPanPolicies.ResolveInertiaProfile(PhotoInertiaProfileDefaults.Standard);

        tuning.Should().Be(PhotoPanInertiaTuning.Default);
    }

    [Fact]
    public void Resolve_ShouldReturnSensitiveTuning()
    {
        var tuning = PhotoPanPolicies.ResolveInertiaProfile(PhotoInertiaProfileDefaults.Sensitive);

        tuning.MouseDecelerationDipPerMs2.Should().Be(0.0026);
        tuning.MouseMaxReleaseSpeedDipPerMs.Should().Be(5.2);
        tuning.MouseMaxDurationMs.Should().Be(980.0);
    }

    [Fact]
    public void Resolve_ShouldReturnHeavyTuning()
    {
        var tuning = PhotoPanPolicies.ResolveInertiaProfile(PhotoInertiaProfileDefaults.Heavy);

        tuning.MouseDecelerationDipPerMs2.Should().Be(0.0016);
        tuning.MouseStopSpeedDipPerMs.Should().Be(0.009);
        tuning.MouseMaxDurationMs.Should().Be(1500.0);
    }
}

public sealed class PhotoPanInteractiveRefreshPolicyTests
{
    [Fact]
    public void ShouldRefresh_ShouldReturnFalse_WhenAccumulatedMovementBelowThreshold()
    {
        var shouldRefresh = PhotoPanPolicies.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 101.5,
            currentTranslateY: 200,
            thresholdDip: 2.0);

        shouldRefresh.Should().BeFalse();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnTrue_WhenAccumulatedXMovementReachesThreshold()
    {
        var shouldRefresh = PhotoPanPolicies.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 102.1,
            currentTranslateY: 200,
            thresholdDip: 2.0);

        shouldRefresh.Should().BeTrue();
    }

    [Fact]
    public void ShouldRefresh_ShouldReturnTrue_WhenAccumulatedYMovementReachesThreshold()
    {
        var shouldRefresh = PhotoPanPolicies.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 100,
            currentTranslateY: 197.9,
            thresholdDip: 2.0);

        shouldRefresh.Should().BeTrue();
    }

    [Fact]
    public void ShouldRefresh_ShouldUseResponsiveDefaultThreshold_WhenNoOverrideProvided()
    {
        var shouldRefresh = PhotoPanPolicies.ShouldRefresh(
            lastRefreshTranslateX: 100,
            lastRefreshTranslateY: 200,
            currentTranslateX: 100.8,
            currentTranslateY: 200);

        shouldRefresh.Should().BeTrue();
    }
}

public sealed class PhotoPanModeSwitchPolicyTests
{
    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenPanNotActive()
    {
        PhotoPanPolicies.ShouldEndPanModeSwitch(
            photoPanning: false,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inkOperationActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenPanStillAllowed()
    {
        PhotoPanPolicies.ShouldEndPanModeSwitch(
            photoPanning: true,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inkOperationActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnTrue_WhenSwitchToDrawMode()
    {
        PhotoPanPolicies.ShouldEndPanModeSwitch(
            photoPanning: true,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inkOperationActive: false).Should().BeTrue();
    }
}

public sealed class PhotoPanMouseExecutionPolicyTests
{
    [Theory]
    [InlineData(0, 0, false)]
    [InlineData(1, 1, true)]
    [InlineData(2, 2, true)]
    public void ResolveMove_ShouldMatchExpected(
        int routingDecision,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = PhotoPanPolicies.ResolveMove((PhotoPanMouseMoveRoutingDecision)routingDecision);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }

    [Theory]
    [InlineData(false, false, 0, false)]
    [InlineData(true, false, 0, false)]
    [InlineData(false, true, 0, false)]
    [InlineData(true, true, 2, true)]
    public void ResolveEnd_ShouldMatchExpected(
        bool isMousePhotoPanActive,
        bool shouldEndPan,
        int expectedAction,
        bool expectedHandled)
    {
        var plan = PhotoPanPolicies.ResolveEnd(
            isMousePhotoPanActive,
            shouldEndPan);

        ((int)plan.Action).Should().Be(expectedAction);
        plan.ShouldMarkHandled.Should().Be(expectedHandled);
    }
}

public sealed class PhotoPanMouseMoveRoutingPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnPassThrough_WhenNotActive()
    {
        var decision = PhotoPanPolicies.ResolveMouseMoveRouting(
            isMousePhotoPanActive: false,
            shouldAllowPhotoPan: true,
            leftButton: MouseButtonState.Pressed,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.PassThrough);
    }

    [Fact]
    public void Resolve_ShouldReturnUpdatePan_WhenActiveAndButtonPressed()
    {
        var decision = PhotoPanPolicies.ResolveMouseMoveRouting(
            isMousePhotoPanActive: true,
            shouldAllowPhotoPan: true,
            leftButton: MouseButtonState.Pressed,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.UpdatePan);
    }

    [Fact]
    public void Resolve_ShouldReturnEndPan_WhenActiveAndNoButtonPressed()
    {
        var decision = PhotoPanPolicies.ResolveMouseMoveRouting(
            isMousePhotoPanActive: true,
            shouldAllowPhotoPan: true,
            leftButton: MouseButtonState.Released,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.EndPan);
    }

    [Fact]
    public void Resolve_ShouldReturnEndPan_WhenActiveButPanNoLongerAllowed()
    {
        var decision = PhotoPanPolicies.ResolveMouseMoveRouting(
            isMousePhotoPanActive: true,
            shouldAllowPhotoPan: false,
            leftButton: MouseButtonState.Pressed,
            rightButton: MouseButtonState.Released);

        decision.Should().Be(PhotoPanMouseMoveRoutingDecision.EndPan);
    }
}

public sealed class PhotoPanMouseRoutingPolicyTests
{
    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnTrue_WhenPanningInPhotoCursorMode()
    {
        PhotoPanPolicies.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: true,
                mode: PaintToolMode.Cursor,
                inkOperationActive: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenNotPanning()
    {
        PhotoPanPolicies.ShouldHandlePhotoPan(
                photoPanning: false,
                photoModeActive: true,
                mode: PaintToolMode.Cursor,
                inkOperationActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenNotPhotoMode()
    {
        PhotoPanPolicies.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: false,
                mode: PaintToolMode.Cursor,
                inkOperationActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenNotCursorMode()
    {
        PhotoPanPolicies.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: true,
                mode: PaintToolMode.Brush,
                inkOperationActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldHandlePhotoPan_ShouldReturnFalse_WhenInkOperationActive()
    {
        PhotoPanPolicies.ShouldHandlePhotoPan(
                photoPanning: true,
                photoModeActive: true,
                mode: PaintToolMode.Cursor,
                inkOperationActive: true)
            .Should()
            .BeFalse();
    }
}

public sealed class PhotoPanReleaseTuningPolicyTests
{
    [Fact]
    public void ResolveTouch_ShouldLowerThreshold_AndIncreaseTravelComparedToMouse()
    {
        var baseTuning = PhotoPanInertiaTuning.Default;

        var mouse = PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, baseTuning);
        var touch = PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Touch, baseTuning);

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

        var mouse = PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Mouse, baseTuning);
        var touch = PhotoPanPolicies.ResolveReleaseTuning(PhotoPanPointerKind.Touch, baseTuning);

        touch.MaxVelocitySampleAgeMs.Should().BeGreaterThan(mouse.MaxVelocitySampleAgeMs);
        touch.VelocitySampleWindowMs.Should().BeGreaterThan(mouse.VelocitySampleWindowMs);
        touch.MinVelocitySampleDistanceDip.Should().BeLessThan(mouse.MinVelocitySampleDistanceDip);
        touch.VelocityRecentWeightGain.Should().BeGreaterThan(mouse.VelocityRecentWeightGain);
    }
}

public sealed class PhotoPanTerminationPolicyTests
{
    [Fact]
    public void ShouldEndPan_ShouldReturnTrue_WhenNoButtonPressed()
    {
        PhotoPanPolicies.ShouldEndPanTermination(
                shouldAllowPhotoPan: true,
                MouseButtonState.Released,
                MouseButtonState.Released)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenLeftPressed()
    {
        PhotoPanPolicies.ShouldEndPanTermination(
                shouldAllowPhotoPan: true,
                MouseButtonState.Pressed,
                MouseButtonState.Released)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnFalse_WhenRightPressed()
    {
        PhotoPanPolicies.ShouldEndPanTermination(
                shouldAllowPhotoPan: true,
                MouseButtonState.Released,
                MouseButtonState.Pressed)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldEndPan_ShouldReturnTrue_WhenPanNoLongerAllowed()
    {
        PhotoPanPolicies.ShouldEndPanTermination(
                shouldAllowPhotoPan: false,
                MouseButtonState.Pressed,
                MouseButtonState.Pressed)
            .Should()
            .BeTrue();
    }
}

public sealed class PhotoViewportStepPolicyTests
{
    [Fact]
    public void ResolveStep_ShouldUseMinStep_ForSmallViewport()
    {
        var value = PhotoPanPolicies.ResolveStep(10);

        value.Should().Be(PhotoPanPolicies.MinStepDip);
    }

    [Fact]
    public void ResolveStep_ShouldUseViewportBasedStep_ForNormalViewport()
    {
        var value = PhotoPanPolicies.ResolveStep(1000);

        value.Should().Be(880);
    }
}
