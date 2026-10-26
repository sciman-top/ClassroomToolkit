using ClassroomToolkit.App.Windowing;
using AwesomeAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class ForegroundZOrderRetouchPolicyTests
{
    [Fact]
    public void ShouldForceOnPresentationFullscreenDetected_ShouldFollowOverlayVisibility()
    {
        ForegroundZOrderRetouchPolicy.ShouldForceOnPresentationFullscreenDetected(true).Should().BeTrue();
        ForegroundZOrderRetouchPolicy.ShouldForceOnPresentationFullscreenDetected(false).Should().BeFalse();
    }
}
