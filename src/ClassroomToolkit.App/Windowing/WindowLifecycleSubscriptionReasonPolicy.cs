namespace ClassroomToolkit.App.Windowing;

internal static class WindowLifecycleSubscriptionReasonPolicy
{
    internal static string ResolveTag(WindowLifecycleSubscriptionReason reason)
    {
        return reason switch
        {
            WindowLifecycleSubscriptionReason.CurrentWindowMissing => "current-window-missing",
            WindowLifecycleSubscriptionReason.SameWindowInstance => "same-window-instance",
            WindowLifecycleSubscriptionReason.WindowInstanceChanged => "window-instance-changed",
            _ => "none"
        };
    }
}
