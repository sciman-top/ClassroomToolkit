using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ImageManagerVisibilityTransitionPlanFactoryTests
{
    [Fact]
    public void CreateOpen_ShouldEnableSurfaceTouch_AndRequestApply()
    {
        var plan = ImageManagerVisibilityTransitionPlanFactory.CreateOpen(
            overlayVisible: true,
            imageManagerVisible: false,
            imageManagerWindowState: WindowState.Minimized);

        plan.SyncOwnersToOverlay.Should().BeTrue();
        plan.ShowWindow.Should().BeTrue();
        plan.NormalizeWindowState.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeTrue();
        plan.TouchImageManagerSurface.Should().BeTrue();
    }

    [Fact]
    public void CreateCloseForPhotoSelection_ShouldClose_WhenVisible()
    {
        var plan = ImageManagerVisibilityTransitionPlanFactory.CreateCloseForPhotoSelection(
            imageManagerVisible: true,
            ownerAlreadyOverlay: true);

        plan.DetachOwnerBeforeClose.Should().BeTrue();
        plan.CloseWindow.Should().BeTrue();
        plan.RequestZOrderApply.Should().BeFalse();
    }
}
