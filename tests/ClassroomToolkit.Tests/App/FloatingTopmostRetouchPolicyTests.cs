using System;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingTopmostRetouchPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenOverlayTopmostTurnsOn()
    {
        var previous = UiSessionState.Default with { OverlayTopmostRequired = false };
        var current = previous with { OverlayTopmostRequired = true };
        var transition = new UiSessionTransition(1, DateTime.UtcNow, new EnterPhotoFullscreenEvent(PhotoSourceKind.Image), previous, current);

        var decision = FloatingTopmostRetouchPolicy.Resolve(transition);
        decision.ShouldEnsureFloatingOnTransition.Should().BeTrue();
        decision.Reason.Should().Be(FloatingTopmostRetouchReason.OverlayTopmostBecameRequired);
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenTopmostStateUnchanged()
    {
        var previous = UiSessionState.Default with { OverlayTopmostRequired = true };
        var current = previous;
        var transition = new UiSessionTransition(2, DateTime.UtcNow, new SwitchToolModeEvent(UiToolMode.Cursor), previous, current);

        var decision = FloatingTopmostRetouchPolicy.Resolve(transition);
        decision.ShouldEnsureFloatingOnTransition.Should().BeFalse();
        decision.Reason.Should().Be(FloatingTopmostRetouchReason.OverlayTopmostNotRising);
    }

    [Fact]
    public void ShouldEnsureFloatingOnTransition_ShouldMapResolveDecision()
    {
        var previous = UiSessionState.Default with { OverlayTopmostRequired = false };
        var current = previous with { OverlayTopmostRequired = true };
        var transition = new UiSessionTransition(3, DateTime.UtcNow, new EnterWhiteboardEvent(), previous, current);

        FloatingTopmostRetouchPolicy.ShouldEnsureFloatingOnTransition(transition).Should().BeTrue();
    }
}
