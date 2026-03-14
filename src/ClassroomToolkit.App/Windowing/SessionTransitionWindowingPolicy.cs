using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal enum SessionTransitionWindowingReason
{
    None = 0,
    EnsureFloatingRequested = 1,
    SceneChanged = 2,
    WidgetBecameVisible = 3,
    NoApplyRequested = 4,
    WidgetVisibilityChangedButNoWidgetBecameVisible = 5
}

internal readonly record struct SessionTransitionWindowingDecision(
    SurfaceZOrderDecision ZOrderDecision,
    SessionTransitionWindowingReason Reason,
    SessionFloatingWidgetVisibilityReason WidgetVisibilityReason,
    SessionTransitionApplyReason ApplyReason,
    SessionTransitionSurfaceReason SurfaceReason);

internal static class SessionTransitionWindowingPolicy
{
    internal static SessionTransitionWindowingDecision ResolveDecision(UiSessionTransition transition)
    {
        var surfaceDecision = SessionTransitionSurfacePolicy.Resolve(
            transition.Previous,
            transition.Current);
        var floatingDecision = FloatingTopmostRetouchPolicy.Resolve(transition);
        var sceneChanged = transition.Previous.Scene != transition.Current.Scene;
        var widgetVisibility = SessionFloatingWidgetVisibilityPolicy.Resolve(
            transition.Previous,
            transition.Current);
        var applyDecision = SessionTransitionApplyPolicy.Resolve(
            floatingDecision.ShouldEnsureFloatingOnTransition,
            transition.Current.OverlayTopmostRequired,
            sceneChanged,
            widgetVisibility);

        var reason = applyDecision.Reason switch
        {
            SessionTransitionApplyReason.EnsureFloatingRequested => SessionTransitionWindowingReason.EnsureFloatingRequested,
            SessionTransitionApplyReason.SceneChanged => SessionTransitionWindowingReason.SceneChanged,
            SessionTransitionApplyReason.WidgetBecameVisible => SessionTransitionWindowingReason.WidgetBecameVisible,
            SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible => SessionTransitionWindowingReason.WidgetVisibilityChangedButNoWidgetBecameVisible,
            _ => SessionTransitionWindowingReason.NoApplyRequested
        };

        return new SessionTransitionWindowingDecision(
            ZOrderDecision: SessionTransitionDecisionFactory.Create(surfaceDecision, applyDecision),
            Reason: reason,
            WidgetVisibilityReason: widgetVisibility.Reason,
            ApplyReason: applyDecision.Reason,
            SurfaceReason: surfaceDecision.Reason);
    }

    internal static SurfaceZOrderDecision Resolve(UiSessionTransition transition)
    {
        return ResolveDecision(transition).ZOrderDecision;
    }
}
