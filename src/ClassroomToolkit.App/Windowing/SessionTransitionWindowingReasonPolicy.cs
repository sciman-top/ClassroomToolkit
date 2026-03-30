namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionWindowingReasonPolicy
{
    internal static string ResolveTag(SessionTransitionWindowingReason reason)
    {
        return reason switch
        {
            SessionTransitionWindowingReason.EnsureFloatingRequested => "ensure-floating-requested",
            SessionTransitionWindowingReason.SceneChanged => "scene-changed",
            SessionTransitionWindowingReason.WidgetBecameVisible => "widget-became-visible",
            SessionTransitionWindowingReason.WidgetVisibilityChangedButNoWidgetBecameVisible => "widget-visibility-changed-without-visible",
            SessionTransitionWindowingReason.NoApplyRequested => "no-apply-requested",
            _ => "none"
        };
    }
}
