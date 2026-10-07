using AwesomeAssertions;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Windowing;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerActivationPolicyTests
{
    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void ShouldOpenOnSingleClick_ShouldOpenFolders_AndPreviewableFiles(
        bool isFolder,
        bool isPdf,
        bool isImage,
        bool expected)
    {
        ImageManagerPolicies.ShouldOpenOnSingleClick(isFolder, isPdf, isImage)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(false, false, true, true)]
    [InlineData(false, false, false, false)]
    public void ShouldOpenOnDoubleClick_ShouldAllowFolderAndPreviewableFiles(
        bool isFolder,
        bool isPdf,
        bool isImage,
        bool expected)
    {
        ImageManagerPolicies.ShouldOpenOnDoubleClick(isFolder, isPdf, isImage)
            .Should()
            .Be(expected);
    }
}

public sealed class ImageManagerStateChangePolicyTests
{
    [Fact]
    public void Resolve_ContextOverload_ShouldRecoverOverlay_WhenBothMinimized()
    {
        var context = new ImageManagerStateChangeContext(
            ImageManagerExists: true,
            ImageManagerWindowState: System.Windows.WindowState.Minimized,
            OverlayVisible: true,
            OverlayWindowState: System.Windows.WindowState.Minimized);

        var decision = ImageManagerWindowingPolicies.ResolveImageManagerStateChange(context);

        decision.NormalizeOverlayWindowState.Should().BeTrue();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldRecoverOverlay_WhenImageManagerMinimizesWhileOverlayMinimized()
    {
        var decision = ImageManagerWindowingPolicies.ResolveImageManagerStateChange(
            imageManagerExists: true,
            imageManagerMinimized: true,
            overlayVisible: true,
            overlayMinimized: true);

        decision.NormalizeOverlayWindowState.Should().BeTrue();
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDoNothing_WhenOverlayNotMinimized()
    {
        var decision = ImageManagerWindowingPolicies.ResolveImageManagerStateChange(
            imageManagerExists: true,
            imageManagerMinimized: true,
            overlayVisible: true,
            overlayMinimized: false);

        decision.NormalizeOverlayWindowState.Should().BeFalse();
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class ImageManagerStateChangeSurfaceDecisionPolicyTests
{
    [Fact]
    public void Resolve_ShouldMapDecisionFlags_ToNoTouchSurfaceDecision()
    {
        var decision = new ImageManagerStateChangeDecision(
            NormalizeOverlayWindowState: true,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true);

        var surfaceDecision = ImageManagerWindowingPolicies.ResolveImageManagerStateChangeSurfaceDecision(decision);

        surfaceDecision.ShouldTouchSurface.Should().BeFalse();
        surfaceDecision.Surface.Should().Be(ZOrderSurface.None);
        surfaceDecision.RequestZOrderApply.Should().BeTrue();
        surfaceDecision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepFlagsFalse_WhenDecisionDoesNotRequestApply()
    {
        var decision = new ImageManagerStateChangeDecision(
            NormalizeOverlayWindowState: false,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var surfaceDecision = ImageManagerWindowingPolicies.ResolveImageManagerStateChangeSurfaceDecision(decision);

        surfaceDecision.ShouldTouchSurface.Should().BeFalse();
        surfaceDecision.RequestZOrderApply.Should().BeFalse();
        surfaceDecision.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class ImageManagerSurfaceTransitionPolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchImageManagerWithoutForce_WhenOpenAndOverlayVisible()
    {
        var decision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.Open,
            overlayVisible: true);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.ImageManager);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldTouchImageManagerWithoutForce_WhenActivated()
    {
        var visibleDecision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.Activated,
            overlayVisible: true);
        var hiddenDecision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.Activated,
            overlayVisible: false);

        visibleDecision.ShouldTouchSurface.Should().BeTrue();
        visibleDecision.Surface.Should().Be(ZOrderSurface.ImageManager);
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeFalse();

        hiddenDecision.ShouldTouchSurface.Should().BeTrue();
        hiddenDecision.Surface.Should().Be(ZOrderSurface.ImageManager);
        hiddenDecision.RequestZOrderApply.Should().BeTrue();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithoutTouch_WhenClosed()
    {
        var visibleDecision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.Closed,
            overlayVisible: true);
        var hiddenDecision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.Closed,
            overlayVisible: false);

        visibleDecision.ShouldTouchSurface.Should().BeFalse();
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeFalse();

        hiddenDecision.ShouldTouchSurface.Should().BeFalse();
        hiddenDecision.RequestZOrderApply.Should().BeTrue();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestApplyWithoutTouch_WhenStateChanged()
    {
        var visibleDecision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.StateChanged,
            overlayVisible: true);
        var hiddenDecision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            ImageManagerSurfaceTransitionKind.StateChanged,
            overlayVisible: false);

        visibleDecision.ShouldTouchSurface.Should().BeFalse();
        visibleDecision.RequestZOrderApply.Should().BeTrue();
        visibleDecision.ForceEnforceZOrder.Should().BeFalse();

        hiddenDecision.ShouldTouchSurface.Should().BeFalse();
        hiddenDecision.RequestZOrderApply.Should().BeFalse();
        hiddenDecision.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldFallbackToNoTouch_ForUnknownKind()
    {
        var decision = ImageManagerWindowingPolicies.ResolveImageManagerSurfaceTransition(
            (ImageManagerSurfaceTransitionKind)999,
            overlayVisible: true);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class ImageManagerTopmostPolicyTests
{
    [Theory]
    [InlineData(true, ZOrderSurface.ImageManager, true)]
    [InlineData(true, ZOrderSurface.PhotoFullscreen, false)]
    [InlineData(false, ZOrderSurface.ImageManager, false)]
    public void Resolve_ShouldMatchExpected(bool visible, ZOrderSurface surface, bool expected)
    {
        var decision = ImageManagerWindowingPolicies.ResolveImageManagerTopmost(visible, surface);
        decision.ShouldApply.Should().Be(expected);
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        ImageManagerWindowingPolicies.ShouldApply(true, ZOrderSurface.ImageManager).Should().BeTrue();
    }
}

public sealed class ImageManagerVisibilitySurfaceDecisionPolicyTests
{
    [Fact]
    public void ResolveOpen_ShouldTouchImageManager_WhenPlanRequestsTouch()
    {
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: true,
            ShowWindow: true,
            NormalizeWindowState: true,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: true,
            TouchImageManagerSurface: true);

        var decision = ImageManagerWindowingPolicies.ResolveOpenImageManagerVisibilitySurfaceDecision(plan);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.ImageManager);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }

    [Fact]
    public void ResolveOpen_ShouldProduceNoTouchDecision_WhenPlanSkipsTouch()
    {
        var plan = new ImageManagerVisibilityTransitionPlan(
            SyncOwnersToOverlay: false,
            ShowWindow: false,
            NormalizeWindowState: false,
            DetachOwnerBeforeClose: false,
            CloseWindow: false,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false,
            TouchImageManagerSurface: false);

        var decision = ImageManagerWindowingPolicies.ResolveOpenImageManagerVisibilitySurfaceDecision(plan);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class ImageManagerVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveOpen_ContextOverload_ShouldReturnSameAsPrimitiveOverload()
    {
        var context = new ImageManagerVisibilityOpenContext(
            OverlayVisible: true,
            ImageManagerVisible: false,
            ImageManagerWindowState: WindowState.Minimized);

        var plan = ImageManagerWindowingPolicies.ResolveOpenImageManagerVisibilityTransition(context);

        plan.SyncOwnersToOverlay.Should().BeTrue();
        plan.ShowWindow.Should().BeTrue();
        plan.NormalizeWindowState.Should().BeTrue();
    }

    [Fact]
    public void ResolveOpen_ShouldShowAndNormalize_WhenWindowIsMinimized()
    {
        var plan = ImageManagerWindowingPolicies.ResolveOpenImageManagerVisibilityTransition(
            overlayVisible: true,
            imageManagerVisible: false,
            imageManagerWindowState: WindowState.Minimized);

        plan.SyncOwnersToOverlay.Should().BeTrue();
        plan.ShowWindow.Should().BeTrue();
        plan.NormalizeWindowState.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeTrue();
        plan.TouchImageManagerSurface.Should().BeTrue();
    }

    [Fact]
    public void ResolveOpen_ShouldSkipShow_WhenWindowAlreadyVisible()
    {
        var plan = ImageManagerWindowingPolicies.ResolveOpenImageManagerVisibilityTransition(
            overlayVisible: false,
            imageManagerVisible: true,
            imageManagerWindowState: WindowState.Normal);

        plan.SyncOwnersToOverlay.Should().BeFalse();
        plan.ShowWindow.Should().BeFalse();
        plan.NormalizeWindowState.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
        plan.TouchImageManagerSurface.Should().BeTrue();
    }

    [Fact]
    public void ResolveCloseForPhotoSelection_ShouldDetachAndClose_WhenWindowVisibleAndOwned()
    {
        var plan = ImageManagerWindowingPolicies.ResolveCloseForPhotoSelection(
            imageManagerVisible: true,
            ownerAlreadyOverlay: true);

        plan.DetachOwnerBeforeClose.Should().BeTrue();
        plan.CloseWindow.Should().BeTrue();
    }

    [Fact]
    public void ResolveCloseForPhotoSelection_ShouldSkipDetach_WhenNotOwnedByOverlay()
    {
        var plan = ImageManagerWindowingPolicies.ResolveCloseForPhotoSelection(
            imageManagerVisible: true,
            ownerAlreadyOverlay: false);

        plan.DetachOwnerBeforeClose.Should().BeFalse();
        plan.CloseWindow.Should().BeTrue();
    }

    [Fact]
    public void ResolveCloseForPhotoSelection_ContextOverload_ShouldCloseWhenVisible()
    {
        var context = new ImageManagerVisibilityCloseContext(
            ImageManagerVisible: true,
            OwnerAlreadyOverlay: false);

        var plan = ImageManagerWindowingPolicies.ResolveCloseForPhotoSelection(context);

        plan.CloseWindow.Should().BeTrue();
    }
}
