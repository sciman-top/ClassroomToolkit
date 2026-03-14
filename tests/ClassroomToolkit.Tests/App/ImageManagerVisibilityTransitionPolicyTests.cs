using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerVisibilityTransitionPolicyTests
{
    [Fact]
    public void ResolveOpen_ContextOverload_ShouldReturnSameAsPrimitiveOverload()
    {
        var context = new ImageManagerVisibilityOpenContext(
            OverlayVisible: true,
            ImageManagerVisible: false,
            ImageManagerWindowState: WindowState.Minimized);

        var plan = ImageManagerVisibilityTransitionPolicy.ResolveOpen(context);

        plan.SyncOwnersToOverlay.Should().BeTrue();
        plan.ShowWindow.Should().BeTrue();
        plan.NormalizeWindowState.Should().BeTrue();
    }

    [Fact]
    public void ResolveOpen_ShouldShowAndNormalize_WhenWindowIsMinimized()
    {
        var plan = ImageManagerVisibilityTransitionPolicy.ResolveOpen(
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
        var plan = ImageManagerVisibilityTransitionPolicy.ResolveOpen(
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
        var plan = ImageManagerVisibilityTransitionPolicy.ResolveCloseForPhotoSelection(
            imageManagerVisible: true,
            ownerAlreadyOverlay: true);

        plan.DetachOwnerBeforeClose.Should().BeTrue();
        plan.CloseWindow.Should().BeTrue();
    }

    [Fact]
    public void ResolveCloseForPhotoSelection_ShouldSkipDetach_WhenNotOwnedByOverlay()
    {
        var plan = ImageManagerVisibilityTransitionPolicy.ResolveCloseForPhotoSelection(
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

        var plan = ImageManagerVisibilityTransitionPolicy.ResolveCloseForPhotoSelection(context);

        plan.CloseWindow.Should().BeTrue();
    }
}
