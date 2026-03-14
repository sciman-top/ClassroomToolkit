using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionApplyGatePolicyTests
{
    [Fact]
    public void Resolve_ShouldReject_WhenDecisionHasNoAction()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var gate = SessionTransitionApplyGatePolicy.Resolve(decision);
        gate.ShouldApply.Should().BeFalse();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.NoZOrderAction);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenTouchSurfaceRequired()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.PhotoFullscreen,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        var gate = SessionTransitionApplyGatePolicy.Resolve(decision);
        gate.ShouldApply.Should().BeTrue();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.TouchSurfaceRequested);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenZOrderApplyRequested()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: true,
            ForceEnforceZOrder: false);

        var gate = SessionTransitionApplyGatePolicy.Resolve(decision);
        gate.ShouldApply.Should().BeTrue();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.ZOrderApplyRequested);
    }

    [Fact]
    public void Resolve_ShouldApply_WhenForceEnforceRequested()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            RequestZOrderApply: false,
            ForceEnforceZOrder: true);

        var gate = SessionTransitionApplyGatePolicy.Resolve(decision);
        gate.ShouldApply.Should().BeTrue();
        gate.Reason.Should().Be(SessionTransitionApplyGateReason.ForceEnforceRequested);
    }

    [Fact]
    public void ShouldApply_ShouldMapResolveDecision()
    {
        var decision = new SurfaceZOrderDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.Whiteboard,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);

        SessionTransitionApplyGatePolicy.ShouldApply(decision).Should().BeTrue();
    }
}
