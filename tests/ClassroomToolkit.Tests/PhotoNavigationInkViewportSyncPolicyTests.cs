using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class PhotoNavigationInkViewportSyncPolicyTests
{
    [Fact]
    public void ResolveAction_ShouldReturnUpdatePanCompensation_WhenPhotoInkModeActiveAndInteractiveSwitch()
    {
        var action = PhotoNavigationInkViewportSyncPolicy.ResolveAction(
            photoInkModeActive: true,
            interactiveSwitch: true);

        action.Should().Be(PhotoNavigationInkViewportSyncAction.UpdatePanCompensation);
    }

    [Fact]
    public void ResolveAction_ShouldReturnResetPanCompensation_WhenPhotoInkModeActiveAndNonInteractiveSwitch()
    {
        var action = PhotoNavigationInkViewportSyncPolicy.ResolveAction(
            photoInkModeActive: true,
            interactiveSwitch: false);

        action.Should().Be(PhotoNavigationInkViewportSyncAction.ResetPanCompensation);
    }

    [Fact]
    public void ResolveAction_ShouldReturnNone_WhenPhotoInkModeInactive()
    {
        var action = PhotoNavigationInkViewportSyncPolicy.ResolveAction(
            photoInkModeActive: false,
            interactiveSwitch: true);

        action.Should().Be(PhotoNavigationInkViewportSyncAction.None);
    }
}
