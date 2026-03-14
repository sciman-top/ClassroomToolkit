using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionSurfacePolicyTests
{
    [Fact]
    public void Resolve_ShouldTouchAndMapSurface_WhenSceneChangesToWhiteboard()
    {
        var previous = UiSessionState.Default;
        var current = previous with { Scene = UiSceneKind.Whiteboard };

        var decision = SessionTransitionSurfacePolicy.Resolve(previous, current);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.Whiteboard);
        decision.Reason.Should().Be(SessionTransitionSurfaceReason.SurfaceRetouchRequested);
    }

    [Fact]
    public void Resolve_ShouldReturnNoTouch_WhenSceneUnchanged()
    {
        var previous = UiSessionState.Default with { Scene = UiSceneKind.PhotoFullscreen };
        var current = previous with { InkDirty = true };

        var decision = SessionTransitionSurfacePolicy.Resolve(previous, current);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.Reason.Should().Be(SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
    }
}
