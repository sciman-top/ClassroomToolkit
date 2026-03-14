using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class PaintWindowVisibilityPolicyTests
{
    [Fact]
    public void ResolveShow_ShouldShowOverlayAttachToolbarAndRestoreMode_WhenToolbarExists()
    {
        var plan = PaintWindowVisibilityPolicy.ResolveShow(
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
        var plan = PaintWindowVisibilityPolicy.ResolveShow(
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
        var plan = PaintWindowVisibilityPolicy.ResolveHide(
            overlayVisible: true,
            toolbarVisible: true);

        plan.HideOverlay.Should().BeTrue();
        plan.HideToolbar.Should().BeTrue();
    }

    [Fact]
    public void ResolveHide_ShouldSkipHide_WhenWindowsAlreadyHidden()
    {
        var plan = PaintWindowVisibilityPolicy.ResolveHide(
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

        var plan = PaintWindowVisibilityPolicy.ResolveShow(context);

        plan.ShowOverlay.Should().BeTrue();
        plan.ToolbarOwnerAction.Should().Be(FloatingOwnerBindingAction.AttachOverlay);
    }

    [Fact]
    public void ResolveHide_ContextOverload_ShouldHideOverlay_WhenVisible()
    {
        var context = new PaintWindowVisibilityHideContext(
            OverlayVisible: true,
            ToolbarVisible: false);

        var plan = PaintWindowVisibilityPolicy.ResolveHide(context);

        plan.HideOverlay.Should().BeTrue();
        plan.HideToolbar.Should().BeFalse();
    }
}
