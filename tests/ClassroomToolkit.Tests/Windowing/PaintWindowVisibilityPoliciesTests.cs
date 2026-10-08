using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class PaintVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveEnsureOverlayVisible_ShouldShowOverlayAndSyncOwners_WhenOverlayHidden()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveEnsureOverlayVisible(overlayVisible: false);

        plan.ShowOverlay.Should().BeTrue();
        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolveEnsureOverlayVisible_ShouldSkipShow_WhenOverlayAlreadyVisible()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveEnsureOverlayVisible(overlayVisible: true);

        plan.ShowOverlay.Should().BeFalse();
        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePaintToggle_ShouldHideOverlayAndCaptureToolbar_WhenOverlayVisible()
    {
        var plan = PaintWindowVisibilityPolicies.ResolvePaintToggle(overlayVisible: true);

        plan.HideOverlay.Should().BeTrue();
        plan.CaptureToolbarPosition.Should().BeTrue();
        plan.SyncFloatingOwnersVisible.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePaintToggle_ShouldShowOverlayAndSyncOwners_WhenOverlayHidden()
    {
        var plan = PaintWindowVisibilityPolicies.ResolvePaintToggle(overlayVisible: false);

        plan.ShowOverlay.Should().BeTrue();
        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePhotoModeChange_ShouldShowToolbarAndNormalize_WhenToolbarMinimized()
    {
        var plan = PaintWindowVisibilityPolicies.ResolvePhotoModeChange(
            photoModeActive: true,
            toolbarWindowState: WindowState.Minimized);

        plan.ShowToolbar.Should().BeTrue();
        plan.NormalizeToolbarWindowState.Should().BeTrue();
        plan.TouchPhotoFullscreenSurface.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePhotoModeChange_ShouldSyncOwners_WhenPhotoModeExits()
    {
        var plan = PaintWindowVisibilityPolicies.ResolvePhotoModeChange(
            photoModeActive: false,
            toolbarWindowState: WindowState.Normal);

        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ShowToolbar.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }
}

public sealed class PaintWindowCreationPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldEnsureWindows_ShouldMatchExpected(
        bool hasOverlayWindow,
        bool hasToolbarWindow,
        bool expected)
    {
        PaintWindowVisibilityPolicies.ShouldEnsureWindows(hasOverlayWindow, hasToolbarWindow)
            .Should()
            .Be(expected);
    }
}

public sealed class PaintWindowEnsureSkipPolicyTests
{
    [Fact]
    public void ShouldSkip_ShouldReturnTrue_WhenAllConditionsSatisfied()
    {
        var shouldSkip = PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false);

        shouldSkip.Should().BeTrue();
    }

    [Fact]
    public void ShouldSkip_ShouldReturnFalse_WhenAnyConditionNotSatisfied()
    {
        PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: false,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false).Should().BeFalse();

        PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: false,
            shouldWireOverlayLifecycle: false,
            shouldWireToolbarLifecycle: false).Should().BeFalse();

        PaintWindowVisibilityPolicies.ShouldSkip(
            hasOverlayWindow: true,
            hasToolbarWindow: true,
            eventsWired: true,
            shouldWireOverlayLifecycle: true,
            shouldWireToolbarLifecycle: false).Should().BeFalse();
    }
}

public sealed class PaintWindowVisibilityPolicyTests
{
    [Fact]
    public void ResolveShow_ShouldShowOverlayAttachToolbarAndRestoreMode_WhenToolbarExists()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveShow(
            overlayVisible: false,
            toolbarExists: true,
            toolbarOwnerAlreadyOverlay: false);

        plan.ShowOverlay.Should().BeTrue();
        plan.ToolbarOwnerAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
        plan.ShowToolbar.Should().BeTrue();
        plan.RestoreToolbarMode.Should().BeTrue();
        plan.RestorePresentationFocus.Should().BeTrue();
    }

    [Fact]
    public void ResolveShow_ShouldSkipToolbarActions_WhenToolbarMissing()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveShow(
            overlayVisible: true,
            toolbarExists: false,
            toolbarOwnerAlreadyOverlay: false);

        plan.ShowOverlay.Should().BeFalse();
        plan.ToolbarOwnerAction.Should().Be(FloatingOwnerBindingAction.None);
        plan.ShowToolbar.Should().BeFalse();
    }

    [Fact]
    public void ResolveHide_ShouldHideVisibleOverlayAndToolbar()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveHide(
            overlayVisible: true,
            toolbarVisible: true);

        plan.HideOverlay.Should().BeTrue();
        plan.HideToolbar.Should().BeTrue();
    }

    [Fact]
    public void ResolveHide_ShouldSkipHide_WhenWindowsAlreadyHidden()
    {
        var plan = PaintWindowVisibilityPolicies.ResolveHide(
            overlayVisible: false,
            toolbarVisible: false);

        plan.HideOverlay.Should().BeFalse();
        plan.HideToolbar.Should().BeFalse();
    }

    [Fact]
    public void ResolveShow_ContextOverload_ShouldShowOverlay_WhenOverlayHidden()
    {
        var context = new PaintWindowVisibilityShowContext(
            OverlayVisible: false,
            ToolbarExists: true,
            ToolbarOwnerAlreadyOverlay: false);

        var plan = PaintWindowVisibilityPolicies.ResolveShow(context);

        plan.ShowOverlay.Should().BeTrue();
        plan.ToolbarOwnerAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void ResolveHide_ContextOverload_ShouldHideOverlay_WhenVisible()
    {
        var context = new PaintWindowVisibilityHideContext(
            OverlayVisible: true,
            ToolbarVisible: false);

        var plan = PaintWindowVisibilityPolicies.ResolveHide(context);

        plan.HideOverlay.Should().BeTrue();
        plan.HideToolbar.Should().BeFalse();
    }
}
