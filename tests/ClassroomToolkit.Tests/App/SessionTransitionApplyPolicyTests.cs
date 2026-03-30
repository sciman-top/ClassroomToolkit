using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SessionTransitionApplyPolicyTests
{
    [Fact]
    public void Resolve_ShouldRequestAndForce_WhenFloatingMustBeEnsured()
    {
        var decision = SessionTransitionApplyPolicy.Resolve(
            shouldEnsureFloating: true,
            overlayTopmostRequired: true,
            sceneChanged: false,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: false,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.None));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionApplyReason.EnsureFloatingRequested);
    }

    [Fact]
    public void Resolve_ShouldRequestWithForce_WhenSceneChangedIntoTopmostMode()
    {
        var decision = SessionTransitionApplyPolicy.Resolve(
            shouldEnsureFloating: false,
            overlayTopmostRequired: true,
            sceneChanged: true,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: false,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.None));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionApplyReason.SceneChanged);
    }

    [Fact]
    public void Resolve_ShouldRequestWithForce_WhenWidgetBecomesVisible()
    {
        var decision = SessionTransitionApplyPolicy.Resolve(
            shouldEnsureFloating: false,
            overlayTopmostRequired: true,
            sceneChanged: false,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: true,
                AnyWidgetBecameVisible: true,
                Reason: SessionFloatingWidgetVisibilityReason.LauncherBecameVisible));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeTrue();
        decision.Reason.Should().Be(SessionTransitionApplyReason.WidgetBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldNotRequest_WhenOnlyWidgetBecomesHidden()
    {
        var decision = SessionTransitionApplyPolicy.Resolve(
            shouldEnsureFloating: false,
            overlayTopmostRequired: false,
            sceneChanged: false,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: true,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible));

        decision.RequestZOrderApply.Should().BeFalse();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible);
    }

    [Fact]
    public void Resolve_ShouldRequestWithoutForce_WhenSceneChangedIntoIdleMode()
    {
        var decision = SessionTransitionApplyPolicy.Resolve(
            shouldEnsureFloating: false,
            overlayTopmostRequired: false,
            sceneChanged: true,
            widgetVisibility: new SessionFloatingWidgetVisibilityDecision(
                AnyVisibilityChanged: false,
                AnyWidgetBecameVisible: false,
                Reason: SessionFloatingWidgetVisibilityReason.None));

        decision.RequestZOrderApply.Should().BeTrue();
        decision.ForceEnforceZOrder.Should().BeFalse();
        decision.Reason.Should().Be(SessionTransitionApplyReason.SceneChanged);
    }
}
