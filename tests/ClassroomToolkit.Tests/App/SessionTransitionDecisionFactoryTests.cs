using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionDecisionFactoryTests
{
    [Fact]
    public void Create_ShouldTouchSurface_WhenSurfaceDecisionRequestsTouch()
    {
        var surfaceDecision = new SessionTransitionSurfaceDecision(
            ShouldTouchSurface: true,
            Surface: ZOrderSurface.Whiteboard,
            Reason: SessionTransitionSurfaceReason.SurfaceRetouchRequested);
        var applyDecision = new SessionTransitionApplyDecision(
            RequestZOrderApply: true,
            ForceEnforceZOrder: false,
            Reason: SessionTransitionApplyReason.SceneChanged);

        var decision = SessionTransitionDecisionFactory.Create(surfaceDecision, applyDecision);

        decision.ShouldTouchSurface.Should().BeTrue();
        decision.Surface.Should().Be(ZOrderSurface.Whiteboard);
        decision.RequestZOrderApply.Should().BeTrue();
    }

    [Fact]
    public void Create_ShouldNoTouch_WhenSurfaceDecisionDoesNotRequestTouch()
    {
        var surfaceDecision = new SessionTransitionSurfaceDecision(
            ShouldTouchSurface: false,
            Surface: ZOrderSurface.None,
            Reason: SessionTransitionSurfaceReason.NoSurfaceRetouchRequested);
        var applyDecision = new SessionTransitionApplyDecision(
            RequestZOrderApply: true,
            ForceEnforceZOrder: true,
            Reason: SessionTransitionApplyReason.EnsureFloatingRequested);

        var decision = SessionTransitionDecisionFactory.Create(surfaceDecision, applyDecision);

        decision.ShouldTouchSurface.Should().BeFalse();
        decision.Surface.Should().Be(ZOrderSurface.None);
        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
    }
}
