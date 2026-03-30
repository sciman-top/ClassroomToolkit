using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherWindowResolverPolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepBubble_WhenPreferredBubbleAndBubbleVisible()
    {
        var kind = LauncherWindowResolverPolicy.Resolve(
            preferredKind: LauncherWindowKind.Bubble,
            bubbleExists: true,
            bubbleVisible: true,
            mainVisible: false);

        kind.Should().Be(LauncherWindowKind.Bubble);
    }

    [Fact]
    public void Resolve_ShouldFallbackToMain_WhenPreferredBubbleButBubbleNotVisibleAndMainVisible()
    {
        var kind = LauncherWindowResolverPolicy.Resolve(
            preferredKind: LauncherWindowKind.Bubble,
            bubbleExists: true,
            bubbleVisible: false,
            mainVisible: true);

        kind.Should().Be(LauncherWindowKind.Main);
    }

    [Fact]
    public void Resolve_ShouldFallbackToVisibleBubble_WhenMainNotVisible()
    {
        var kind = LauncherWindowResolverPolicy.Resolve(
            preferredKind: LauncherWindowKind.Main,
            bubbleExists: true,
            bubbleVisible: true,
            mainVisible: false);

        kind.Should().Be(LauncherWindowKind.Bubble);
    }

    [Fact]
    public void Resolve_ShouldStayMain_WhenBothHiddenAndBubbleMissing()
    {
        var kind = LauncherWindowResolverPolicy.Resolve(
            preferredKind: LauncherWindowKind.Bubble,
            bubbleExists: false,
            bubbleVisible: false,
            mainVisible: false);

        kind.Should().Be(LauncherWindowKind.Main);
    }
}
