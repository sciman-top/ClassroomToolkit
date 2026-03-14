using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionFloatingWidgetVisibilityPolicyTests
{
    [Fact]
    public void Resolve_ShouldDetectVisibilityChange_WhenAnyWidgetChanges()
    {
        var previous = UiSessionState.Default with
        {
            RollCallVisible = false,
            LauncherVisible = false,
            ToolbarVisible = false
        };
        var current = previous with
        {
            LauncherVisible = true
        };

        var decision = SessionFloatingWidgetVisibilityPolicy.Resolve(previous, current);

        decision.AnyVisibilityChanged.Should().BeTrue();
        decision.AnyWidgetBecameVisible.Should().BeTrue();
        decision.Reason.Should().Be(SessionFloatingWidgetVisibilityReason.LauncherBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldDetectChangeWithoutBecomeVisible_WhenWidgetHides()
    {
        var previous = UiSessionState.Default with
        {
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };
        var current = previous with
        {
            LauncherVisible = false
        };

        var decision = SessionFloatingWidgetVisibilityPolicy.Resolve(previous, current);

        decision.AnyVisibilityChanged.Should().BeTrue();
        decision.AnyWidgetBecameVisible.Should().BeFalse();
        decision.Reason.Should().Be(SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldReturnNoChanges_WhenVisibilityStateMatches()
    {
        var state = UiSessionState.Default with
        {
            RollCallVisible = true,
            LauncherVisible = true,
            ToolbarVisible = true
        };

        var decision = SessionFloatingWidgetVisibilityPolicy.Resolve(state, state);

        decision.AnyVisibilityChanged.Should().BeFalse();
        decision.AnyWidgetBecameVisible.Should().BeFalse();
        decision.Reason.Should().Be(SessionFloatingWidgetVisibilityReason.None);
    }
}
