namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SessionTransitionApplyDecision(
    bool RequestZOrderApply,
    bool ForceEnforceZOrder,
    SessionTransitionApplyReason Reason);

internal enum SessionTransitionApplyReason
{
    None = 0,
    EnsureFloatingRequested = 1,
    SceneChanged = 2,
    WidgetBecameVisible = 3,
    NoApplyRequested = 4,
    WidgetVisibilityChangedButNoWidgetBecameVisible = 5
}

internal static class SessionTransitionApplyPolicy
{
    internal static SessionTransitionApplyDecision Resolve(
        bool shouldEnsureFloating,
        bool overlayTopmostRequired,
        bool sceneChanged,
        SessionFloatingWidgetVisibilityDecision widgetVisibility)
    {
        var reason = shouldEnsureFloating
            ? SessionTransitionApplyReason.EnsureFloatingRequested
            : sceneChanged
                ? SessionTransitionApplyReason.SceneChanged
                : widgetVisibility.AnyWidgetBecameVisible
                    ? SessionTransitionApplyReason.WidgetBecameVisible
                    : widgetVisibility.AnyVisibilityChanged
                        ? SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible
                    : SessionTransitionApplyReason.NoApplyRequested;
        return new SessionTransitionApplyDecision(
            RequestZOrderApply: shouldEnsureFloating || sceneChanged || widgetVisibility.AnyWidgetBecameVisible,
            ForceEnforceZOrder: shouldEnsureFloating
                || widgetVisibility.AnyWidgetBecameVisible
                || (sceneChanged && overlayTopmostRequired),
            Reason: reason);
    }
}
