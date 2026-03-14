using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedDedupIntervalPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldReturnInteractiveWindow_WhenPhotoOrWhiteboardActive()
    {
        LauncherBubbleVisibleChangedDedupIntervalPolicy.ResolveMs(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false).Should().Be(130);

        LauncherBubbleVisibleChangedDedupIntervalPolicy.ResolveMs(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true).Should().Be(130);
    }

    [Fact]
    public void ResolveMs_ShouldReturnDefaultWindow_WhenOverlayNotInteractive()
    {
        LauncherBubbleVisibleChangedDedupIntervalPolicy.ResolveMs(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true).Should().Be(90);
    }
}
