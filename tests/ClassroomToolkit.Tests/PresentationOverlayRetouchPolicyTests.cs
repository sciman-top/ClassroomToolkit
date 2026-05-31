using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PresentationOverlayRetouchPolicyTests
{
    [Fact]
    public void ShouldRequest_ShouldReturnTrue_WhenPresentationActionCanCoverVisibleFullscreenOverlay()
    {
        var result = PresentationOverlayRetouchPolicy.ShouldRequest(
            presentationActionApplied: true,
            overlayVisible: true,
            presentationFullscreenActive: true);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    public void ShouldRequest_ShouldReturnFalse_WhenRetouchWouldBeNoise(
        bool presentationActionApplied,
        bool overlayVisible,
        bool presentationFullscreenActive)
    {
        var result = PresentationOverlayRetouchPolicy.ShouldRequest(
            presentationActionApplied,
            overlayVisible,
            presentationFullscreenActive);

        result.Should().BeFalse();
    }
}
