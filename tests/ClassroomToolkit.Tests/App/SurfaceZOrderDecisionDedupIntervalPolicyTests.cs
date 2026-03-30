using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SurfaceZOrderDecisionDedupIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldReturnInteractiveWindow_WhenPhotoOrWhiteboardActive()
    {
        SurfaceZOrderDecisionDedupIntervalPolicy.ResolveMs(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false).Should().Be(130);

        SurfaceZOrderDecisionDedupIntervalPolicy.ResolveMs(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true).Should().Be(130);
    }

    [Fact]
    public void ResolveMs_ShouldReturnDefaultWindow_WhenOverlayNotInteractive()
    {
        SurfaceZOrderDecisionDedupIntervalPolicy.ResolveMs(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true).Should().Be(90);
    }
}
