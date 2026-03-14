namespace ClassroomToolkit.App.Windowing;

internal static class WindowTopmostInteropRetryReasonPolicy
{
    internal static string ResolveTag(WindowTopmostInteropRetryReason reason)
    {
        return reason switch
        {
            WindowTopmostInteropRetryReason.MaxAttemptsReached => "max-attempts-reached",
            WindowTopmostInteropRetryReason.InvalidHandleError => "invalid-handle-error",
            WindowTopmostInteropRetryReason.RetryableError => "retryable-error",
            _ => "none"
        };
    }
}
