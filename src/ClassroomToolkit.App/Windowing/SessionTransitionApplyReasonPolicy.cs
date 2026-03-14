namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionApplyReasonPolicy
{
    internal static string ResolveTag(SessionTransitionApplyReason reason)
    {
        return reason switch
        {
            SessionTransitionApplyReason.EnsureFloatingRequested => "ensure-floating-requested",
            SessionTransitionApplyReason.SceneChanged => "scene-changed",
            SessionTransitionApplyReason.WidgetBecameVisible => "widget-became-visible",
            SessionTransitionApplyReason.WidgetVisibilityChangedButNoWidgetBecameVisible => "widget-visibility-changed-without-visible",
            SessionTransitionApplyReason.NoApplyRequested => "no-apply-requested",
            _ => "none"
        };
    }
}
