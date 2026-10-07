using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Windowing;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class PhotoCloseOwnerDetachmentPolicyTests
{
    [Fact]
    public void ShouldDetachOwners_ShouldReturnTrue_WhenSyncOwnersNotVisible()
    {
        PhotoOverlayTransitionsPolicies.ShouldDetachOwners(syncFloatingOwnersVisible: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldDetachOwners_ShouldReturnFalse_WhenSyncOwnersVisible()
    {
        PhotoOverlayTransitionsPolicies.ShouldDetachOwners(syncFloatingOwnersVisible: true).Should().BeFalse();
    }
}

public sealed class PhotoCloseTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldDetachOwnersAndRequestForcedZOrder_WhenOverlayVisible()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoCloseTransition(overlayVisible: true);

        plan.SyncFloatingOwnersVisible.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldOnlyDetachOwners_WhenOverlayNotVisible()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoCloseTransition(overlayVisible: false);

        plan.SyncFloatingOwnersVisible.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ContextOverload_ShouldMatchOverlayVisibleBehavior()
    {
        var context = new PhotoCloseTransitionContext(OverlayVisible: true);

        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoCloseTransition(context);

        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
    }
}

public sealed class PhotoCursorModeFocusRequestPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnFocusRequested_WhenPhotoModeAndCursorMode()
    {
        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoCursorModeFocusRequest(
            photoModeActive: true,
            mode: PaintToolMode.Cursor);

        decision.ShouldRequestFocus.Should().BeTrue();
        decision.Reason.Should().Be(PhotoCursorModeFocusRequestReason.FocusRequested);
    }

    [Theory]
    [InlineData(false, PaintToolMode.Cursor)]
    [InlineData(true, PaintToolMode.Brush)]
    [InlineData(true, PaintToolMode.Eraser)]
    public void Resolve_ShouldReturnFalse_WhenGuardsNotSatisfied(
        bool photoModeActive,
        PaintToolMode mode)
    {
        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoCursorModeFocusRequest(
            photoModeActive: photoModeActive,
            mode: mode);

        decision.ShouldRequestFocus.Should().BeFalse();
    }

    [Fact]
    public void ShouldRequestFocus_ShouldMapResolveDecision()
    {
        var shouldRequest = PhotoOverlayTransitionsPolicies.ShouldRequestFocus(
            photoModeActive: true,
            mode: PaintToolMode.Cursor);

        shouldRequest.Should().BeTrue();
    }
}

public sealed class PhotoModeSurfaceTransitionPolicyTests
{
    [Fact]
    public void ResolvePhotoModeChanged_ShouldNotForceByDefault()
    {
        var activeDecision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            photoModeActive: true,
            requestZOrderApply: true,
            forceEnforceZOrder: false,
            overlayVisible: false);
        var inactiveDecision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            photoModeActive: false,
            requestZOrderApply: true,
            forceEnforceZOrder: false,
            overlayVisible: false);

        activeDecision.ShouldTouchSurface.Should().BeTrue();
        activeDecision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        activeDecision.RequestZOrderApply.Should().BeTrue();
        activeDecision.ForceEnforceZOrder.Should().BeFalse();

        inactiveDecision.ShouldTouchSurface.Should().BeFalse();
        inactiveDecision.Surface.Should().Be(ZOrderSurface.None);
        inactiveDecision.RequestZOrderApply.Should().BeTrue();
        inactiveDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePhotoModeChanged_ShouldHonorExplicitForce_WhenPhotoModeInactive()
    {
        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            photoModeActive: false,
            requestZOrderApply: true,
            forceEnforceZOrder: true,
            overlayVisible: false);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolvePresentationFullscreenDetected_ShouldFollowOverlayVisibility()
    {
        var visibleDecision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected,
            overlayVisible: true,
            photoModeActive: false,
            requestZOrderApply: false,
            forceEnforceZOrder: false);
        var hiddenDecision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PresentationFullscreenDetected,
            overlayVisible: false,
            photoModeActive: false,
            requestZOrderApply: false,
            forceEnforceZOrder: false);

        visibleDecision.ShouldTouchSurface.Should().BeFalse();
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeTrue();

        hiddenDecision.ShouldTouchSurface.Should().BeFalse();
        hiddenDecision.RequestZOrderApply.Should().BeFalse();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldFallbackToNoTouch_ForUnknownKind()
    {
        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            (PhotoModeSurfaceTransitionKind)999,
            photoModeActive: true,
            requestZOrderApply: true,
            forceEnforceZOrder: true,
            overlayVisible: true);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ContextOverload_ShouldMatchPrimitiveBehavior()
    {
        var context = new PhotoModeSurfaceTransitionContext(
            PhotoModeActive: true,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false,
            OverlayVisible: false);

        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoModeSurfaceTransition(
            PhotoModeSurfaceTransitionKind.PhotoModeChanged,
            context);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class PhotoOverlayEntryPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnEntryPlan_WhenPathExists()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayEntry(hasPath: true);

        plan.UpdateSequence.Should().BeTrue();
        plan.UpdateInkVisibility.Should().BeTrue();
        plan.SuppressNextOverlayActivatedApply.Should().BeTrue();
        plan.EnterPhotoMode.Should().BeTrue();
        plan.TouchPhotoSurface.Should().BeTrue();
        plan.FocusOverlay.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnSequenceOnly_WhenPathMissing()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayEntry(hasPath: false);

        plan.UpdateSequence.Should().BeTrue();
        plan.UpdateInkVisibility.Should().BeFalse();
        plan.EnterPhotoMode.Should().BeFalse();
        plan.TouchPhotoSurface.Should().BeFalse();
        plan.FocusOverlay.Should().BeFalse();
    }
}

public sealed class PhotoOverlayEntrySurfaceTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchPhotoSurface_WhenEntryPlanRequestsPhotoSurface()
    {
        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayEntrySurfaceTransition(touchPhotoSurface: true);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.PhotoFullscreen);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRemainNoop_WhenEntryPlanDoesNotTouchPhotoSurface()
    {
        var decision = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayEntrySurfaceTransition(touchPhotoSurface: false);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}

public class PhotoOverlayReentryPolicyTests
{
    [Fact]
    public void Resolve_ShouldNormalizeAndActivate_WhenReenteringSamePhotoFromMinimizedState()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayReentry(
            windowMinimized: true,
            photoModeActive: true,
            sameSourcePath: true);

        plan.NormalizeWindowState.Should().BeTrue();
        plan.ActivateOverlay.Should().BeTrue();
        plan.ReturnEarly.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotReturnEarly_WhenPathChanges()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayReentry(
            windowMinimized: false,
            photoModeActive: true,
            sameSourcePath: false);

        plan.NormalizeWindowState.Should().BeFalse();
        plan.ActivateOverlay.Should().BeFalse();
        plan.ReturnEarly.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldOnlyNormalize_WhenEnteringNewPhotoFromMinimizedState()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoOverlayReentry(
            windowMinimized: true,
            photoModeActive: false,
            sameSourcePath: false);

        plan.NormalizeWindowState.Should().BeTrue();
        plan.ActivateOverlay.Should().BeFalse();
        plan.ReturnEarly.Should().BeFalse();
    }
}

public sealed class PhotoSelectionPreparationPolicyTests
{
    [Fact]
    public void Resolve_ShouldCloseImageManager_AndDisableWhiteboard_WhenNeeded()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoSelectionPreparation(
            imageManagerVisible: true,
            whiteboardActive: true);

        plan.CloseImageManager.Should().BeTrue();
        plan.DisableWhiteboard.Should().BeTrue();
        plan.SuppressPresentationForeground.Should().BeTrue();
        plan.PresentationForegroundSuppressionMs.Should().Be(PhotoSelectionPreparationDefaults.PresentationForegroundSuppressionMs);
    }

    [Fact]
    public void Resolve_ShouldStillSuppressForeground_WhenNoOtherCleanupNeeded()
    {
        var plan = PhotoOverlayTransitionsPolicies.ResolvePhotoSelectionPreparation(
            imageManagerVisible: false,
            whiteboardActive: false);

        plan.CloseImageManager.Should().BeFalse();
        plan.DisableWhiteboard.Should().BeFalse();
        plan.SuppressPresentationForeground.Should().BeTrue();
    }
}
