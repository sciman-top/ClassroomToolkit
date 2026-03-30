using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherBubbleVisibleChangedSuppressionPolicyTests
{
    [Fact]
    public void ResolveCooldownMs_ShouldReturnDefault_WhenNotInteractiveScene()
    {
        var cooldown = LauncherBubbleVisibleChangedSuppressionPolicy.ResolveCooldownMs(
            overlayVisible: false,
            photoModeActive: true,
            whiteboardActive: true);

        cooldown.Should().Be(LauncherBubbleVisibleChangedSuppressionDefaults.TransitionCooldownMs);
    }

    [Fact]
    public void ResolveCooldownMs_ShouldReturnInteractive_WhenPhotoOrWhiteboardInOverlay()
    {
        var photoCooldown = LauncherBubbleVisibleChangedSuppressionPolicy.ResolveCooldownMs(
            overlayVisible: true,
            photoModeActive: true,
            whiteboardActive: false);
        var boardCooldown = LauncherBubbleVisibleChangedSuppressionPolicy.ResolveCooldownMs(
            overlayVisible: true,
            photoModeActive: false,
            whiteboardActive: true);

        photoCooldown.Should().Be(LauncherBubbleVisibleChangedSuppressionDefaults.InteractiveTransitionCooldownMs);
        boardCooldown.Should().Be(LauncherBubbleVisibleChangedSuppressionDefaults.InteractiveTransitionCooldownMs);
    }
}
