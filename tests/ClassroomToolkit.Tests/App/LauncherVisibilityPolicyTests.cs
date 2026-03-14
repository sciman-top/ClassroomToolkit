using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherVisibilityPolicyTests
{
    [Fact]
    public void ResolveForTopmost_ShouldUseMainWindow_WhenNotMinimizedMode()
    {
        var decision = LauncherVisibilityPolicy.ResolveForTopmost(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: true,
            bubbleMinimized: false);

        decision.IsVisible.Should().BeTrue();
        decision.Reason.Should().Be(LauncherTopmostVisibilityReason.MainVisible);
    }

    [Fact]
    public void ResolveForTopmost_ShouldUseBubble_WhenMinimizedMode()
    {
        var decision = LauncherVisibilityPolicy.ResolveForTopmost(
            launcherMinimized: true,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: true,
            bubbleMinimized: false);

        decision.IsVisible.Should().BeTrue();
        decision.Reason.Should().Be(LauncherTopmostVisibilityReason.BubbleVisible);
    }

    [Fact]
    public void ResolveForTopmost_ShouldReturnFalse_WhenSelectedWindowIsMinimized()
    {
        var mainMode = LauncherVisibilityPolicy.ResolveForTopmost(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: true,
            bubbleVisible: true,
            bubbleMinimized: false);

        var bubbleMode = LauncherVisibilityPolicy.ResolveForTopmost(
            launcherMinimized: true,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: true,
            bubbleMinimized: true);

        mainMode.IsVisible.Should().BeFalse();
        mainMode.Reason.Should().Be(LauncherTopmostVisibilityReason.MainHiddenOrMinimized);
        bubbleMode.IsVisible.Should().BeFalse();
        bubbleMode.Reason.Should().Be(LauncherTopmostVisibilityReason.BubbleHiddenOrMinimized);
    }

    [Fact]
    public void IsVisibleForTopmost_ShouldMapResolveDecision()
    {
        LauncherVisibilityPolicy.IsVisibleForTopmost(
            launcherMinimized: false,
            mainVisible: true,
            mainMinimized: false,
            bubbleVisible: false,
            bubbleMinimized: false).Should().BeTrue();
    }
}
