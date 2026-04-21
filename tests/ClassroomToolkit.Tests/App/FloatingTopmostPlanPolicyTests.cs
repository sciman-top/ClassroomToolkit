using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableImageManagerTopmost_WhenImageManagerIsFront()
    {
        var plan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface: ZOrderSurface.ImageManager,
            toolbarVisible: true,
            rollCallVisible: true,
            launcherVisible: true,
            imageManagerVisible: true,
            overlayVisible: true);

        plan.ImageManagerTopmost.Should().BeTrue();
        plan.OverlayShouldActivate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestOverlayActivation_WhenPhotoSurfaceHasNoVisibleFloatingUtility()
    {
        var plan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface: ZOrderSurface.PhotoFullscreen,
            toolbarVisible: false,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            overlayVisible: true);

        plan.OverlayShouldActivate.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldNotRequestOverlayActivation_WhenPhotoSurfaceHasVisibleFloatingUtility()
    {
        var plan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface: ZOrderSurface.PhotoFullscreen,
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: true,
            imageManagerVisible: false,
            overlayVisible: true);

        plan.OverlayShouldActivate.Should().BeFalse();
        plan.ImageManagerTopmost.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldRequestOverlayActivation_WhenWhiteboardIsFront()
    {
        var plan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface: ZOrderSurface.Whiteboard,
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: true,
            imageManagerVisible: true,
            overlayVisible: true);

        plan.OverlayShouldActivate.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableTopmostFlags_WhenWindowsAreHidden()
    {
        var plan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface: ZOrderSurface.None,
            toolbarVisible: false,
            rollCallVisible: false,
            launcherVisible: false,
            imageManagerVisible: false,
            overlayVisible: false);

        plan.ToolbarTopmost.Should().BeFalse();
        plan.RollCallTopmost.Should().BeFalse();
        plan.LauncherTopmost.Should().BeFalse();
        plan.ImageManagerTopmost.Should().BeFalse();
        plan.OverlayShouldActivate.Should().BeFalse();
    }

    [Theory]
    [InlineData(ZOrderSurface.PhotoFullscreen)]
    [InlineData(ZOrderSurface.Whiteboard)]
    [InlineData(ZOrderSurface.PresentationFullscreen)]
    [InlineData(ZOrderSurface.ImageManager)]
    public void Resolve_ShouldKeepLauncherTopmost_WhenLauncherVisible(ZOrderSurface surface)
    {
        var plan = FloatingTopmostPlanPolicy.Resolve(
            frontSurface: surface,
            toolbarVisible: true,
            rollCallVisible: true,
            launcherVisible: true,
            imageManagerVisible: true,
            overlayVisible: true);

        plan.LauncherTopmost.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldSupportVisibilitySnapshotInput()
    {
        var visibility = FloatingTopmostVisibilitySnapshotPolicy.Resolve(
            toolbarVisible: true,
            rollCallVisible: false,
            launcherVisible: true,
            imageManagerVisible: false,
            overlayVisible: true);

        var plan = FloatingTopmostPlanPolicy.Resolve(
            ZOrderSurface.Whiteboard,
            visibility);

        plan.ToolbarTopmost.Should().BeTrue();
        plan.RollCallTopmost.Should().BeFalse();
        plan.LauncherTopmost.Should().BeTrue();
        plan.ImageManagerTopmost.Should().BeFalse();
        plan.OverlayShouldActivate.Should().BeTrue();
    }
}
