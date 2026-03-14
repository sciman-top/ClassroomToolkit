using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SessionFloatingWidgetVisibilityDecision(
    bool AnyVisibilityChanged,
    bool AnyWidgetBecameVisible,
    SessionFloatingWidgetVisibilityReason Reason);

internal enum SessionFloatingWidgetVisibilityReason
{
    None = 0,
    RollCallBecameVisible = 1,
    LauncherBecameVisible = 2,
    ToolbarBecameVisible = 3,
    VisibilityChangedButNoWidgetBecameVisible = 4
}

internal static class SessionFloatingWidgetVisibilityPolicy
{
    internal static SessionFloatingWidgetVisibilityDecision Resolve(
        UiSessionState previous,
        UiSessionState current)
    {
        var rollCallChanged = previous.RollCallVisible != current.RollCallVisible;
        var launcherChanged = previous.LauncherVisible != current.LauncherVisible;
        var toolbarChanged = previous.ToolbarVisible != current.ToolbarVisible;
        var anyChanged = rollCallChanged || launcherChanged || toolbarChanged;

        var anyBecameVisible =
            (!previous.RollCallVisible && current.RollCallVisible)
            || (!previous.LauncherVisible && current.LauncherVisible)
            || (!previous.ToolbarVisible && current.ToolbarVisible);

        var reason = !anyChanged
            ? SessionFloatingWidgetVisibilityReason.None
            : !previous.RollCallVisible && current.RollCallVisible
                ? SessionFloatingWidgetVisibilityReason.RollCallBecameVisible
                : !previous.LauncherVisible && current.LauncherVisible
                    ? SessionFloatingWidgetVisibilityReason.LauncherBecameVisible
                    : !previous.ToolbarVisible && current.ToolbarVisible
                        ? SessionFloatingWidgetVisibilityReason.ToolbarBecameVisible
                        : SessionFloatingWidgetVisibilityReason.VisibilityChangedButNoWidgetBecameVisible;

        return new SessionFloatingWidgetVisibilityDecision(
            AnyVisibilityChanged: anyChanged,
            AnyWidgetBecameVisible: anyBecameVisible,
            Reason: reason);
    }
}
