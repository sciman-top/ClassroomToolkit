namespace ClassroomToolkit.App.Windowing;

internal static class WindowPlacementInteropRetryReasonPolicy
{
    internal static string ResolveTag(WindowPlacementInteropRetryReason reason)
    {
        return reason switch
        {
            WindowPlacementInteropRetryReason.MaxAttemptsReached => "max-attempts-reached",
            WindowPlacementInteropRetryReason.InvalidHandleError => "invalid-handle-error",
            WindowPlacementInteropRetryReason.RetryableError => "retryable-error",
            _ => "none"
        };
    }
}
