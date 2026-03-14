namespace ClassroomToolkit.App.Windowing;

internal static class WindowStyleInteropRetryReasonPolicy
{
    internal static string ResolveTag(WindowStyleInteropRetryReason reason)
    {
        return reason switch
        {
            WindowStyleInteropRetryReason.MaxAttemptsReached => "max-attempts-reached",
            WindowStyleInteropRetryReason.InvalidHandleError => "invalid-handle-error",
            WindowStyleInteropRetryReason.RetryableError => "retryable-error",
            _ => "none"
        };
    }
}
