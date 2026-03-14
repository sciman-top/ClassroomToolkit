namespace ClassroomToolkit.App.Windowing;

internal static class SessionFloatingWidgetVisibilityReasonPolicy
{
    internal static string ResolveTag(SessionFloatingWidgetVisibilityReason reason)
    {
        return reason switch
        {
            SessionFloatingWidgetVisibilityReason.RollCallBecameVisible => "rollcall-became-visible",
            SessionFloatingWidgetVisibilityReason.LauncherBecameVisible => "launcher-became-visible",
            SessionFloatingWidgetVisibilityReason.ToolbarBecameVisible => "toolbar-became-visible",
            SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible => "visibility-changed-without-visible",
            _ => "none"
        };
    }
}
