using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveEnsureOverlayVisible_ShouldShowOverlayAndSyncOwners_WhenOverlayHidden()
    {
        var plan = PaintVisibilityTransitionPolicy.ResolveEnsureOverlayVisible(overlayVisible: false);

        plan.ShowOverlay.Should().BeTrue();
        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolveEnsureOverlayVisible_ShouldSkipShow_WhenOverlayAlreadyVisible()
    {
        var plan = PaintVisibilityTransitionPolicy.ResolveEnsureOverlayVisible(overlayVisible: true);

        plan.ShowOverlay.Should().BeFalse();
        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePaintToggle_ShouldHideOverlayAndCaptureToolbar_WhenOverlayVisible()
    {
        var plan = PaintVisibilityTransitionPolicy.ResolvePaintToggle(overlayVisible: true);

        plan.HideOverlay.Should().BeTrue();
        plan.CaptureToolbarPosition.Should().BeTrue();
        plan.SyncFloatingOwnersVisible.Should().BeFalse();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePaintToggle_ShouldShowOverlayAndSyncOwners_WhenOverlayHidden()
    {
        var plan = PaintVisibilityTransitionPolicy.ResolvePaintToggle(overlayVisible: false);

        plan.ShowOverlay.Should().BeTrue();
        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }

    [Fact]
    public void ResolvePhotoModeChange_ShouldShowToolbarAndNormalize_WhenToolbarMinimized()
    {
        var plan = PaintVisibilityTransitionPolicy.ResolvePhotoModeChange(
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
        var plan = PaintVisibilityTransitionPolicy.ResolvePhotoModeChange(
            photoModeActive: false,
            toolbarWindowState: WindowState.Normal);

        plan.SyncFloatingOwnersVisible.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.ShowToolbar.Should().BeFalse();
        plan.ForceEnforceZOrder.Should().BeFalse();
    }
}
