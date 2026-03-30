using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionZOrderPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenSceneChangesToNonIdle()
    {
        var previous = UiSessionState.Default;
        var current = UiSessionReducer.Reduce(previous, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));

        var decision = SessionTransitionZOrderPolicy.Resolve(previous, current);
        decision.ShouldRetouchSurface.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneChangedToSurface);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenSceneUnchanged()
    {
        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterWhiteboardEvent());
        var current = UiSessionReducer.Reduce(previous, new SwitchToolModeEvent(UiToolMode.Cursor));

        var decision = SessionTransitionZOrderPolicy.Resolve(previous, current);
        decision.ShouldRetouchSurface.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneUnchanged);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenSceneChangesToIdle()
    {
        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterPresentationFullscreenEvent(PresentationSourceKind.PowerPoint));
        var current = UiSessionReducer.Reduce(previous, new ExitPresentationFullscreenEvent());

        var decision = SessionTransitionZOrderPolicy.Resolve(previous, current);
        decision.ShouldRetouchSurface.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneChangedToNoneSurface);
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenSceneChangesBetweenNonIdleScenes()
    {
        var previous = UiSessionReducer.Reduce(UiSessionState.Default, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));
        var current = UiSessionReducer.Reduce(previous, new EnterWhiteboardEvent());

        var decision = SessionTransitionZOrderPolicy.Resolve(previous, current);
        decision.ShouldRetouchSurface.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionZOrderRetouchReason.SceneChangedToSurface);
    }

    [Fact]
    public void ShouldRetouchSurface_ShouldMapResolveDecision()
    {
        var previous = UiSessionState.Default;
        var current = UiSessionReducer.Reduce(previous, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image));

        SessionTransitionZOrderPolicy.ShouldRetouchSurface(previous, current).Should().BeTrue();
    }
}
