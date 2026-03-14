using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundZOrderRetouchPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnOverlayVisiblePresentation_WhenPresentationAndOverlayVisible()
    {
        var decision = ForegroundZOrderRetouchPolicy.Resolve(
            ForegroundZOrderRetouchTrigger.PresentationFullscreenDetected,
            overlayVisible: true);

        decision.ShouldForce.Should().BeTrue();
        decision.Reason.Should().Be(ForegroundZOrderRetouchReason.OverlayVisiblePresentation);
    }

    [Fact]
    public void Resolve_ShouldReturnDisabledByDesign_ForNonPresentationTrigger()
    {
        var decision = ForegroundZOrderRetouchPolicy.Resolve(
            ForegroundZOrderRetouchTrigger.ImageManagerActivated,
            overlayVisible: true);

        decision.ShouldForce.Should().BeFalse();
        decision.Reason.Should().Be(ForegroundZOrderRetouchReason.ForceDisabledByDesign);
    }

    [Fact]
    public void ShouldForceOnToolbarInteraction_ShouldRemainDisabled()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnToolbarInteraction(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnToolbarInteraction(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnToolbarInteraction(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: false).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnToolbarInteraction(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldForceOnImageManagerActivated_ShouldFollowOverlayVisibility()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerActivated(true).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerActivated(false).Should().BeFalse();
    }

    [Fact]
    public void ShouldForceOnImageManagerClosed_ShouldFollowOverlayVisibility()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerClosed(true).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerClosed(false).Should().BeFalse();
    }

    [Fact]
    public void ShouldForceOnImageManagerStateChanged_ShouldFollowOverlayVisibility()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerStateChanged(true).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnImageManagerStateChanged(false).Should().BeFalse();
    }

    [Fact]
    public void ShouldForceOnPhotoModeChanged_ShouldForceOnlyWhenPhotoModeActive()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnPhotoModeChanged(true).Should().BeFalse();
        ForegroundZOrderRetouchPolicy.ShouldForceOnPhotoModeChanged(false).Should().BeFalse();
    }

    [Fact]
    public void ShouldForceOnPresentationFullscreenDetected_ShouldFollowOverlayVisibility()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnPresentationFullscreenDetected(true).Should().BeTrue();
        ForegroundZOrderRetouchPolicy.ShouldForceOnPresentationFullscreenDetected(false).Should().BeFalse();
    }
}
