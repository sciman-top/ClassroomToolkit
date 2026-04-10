namespace ClassroomToolkit.App.Windowing;

internal enum WindowTopmostInteropRetryReason
{
    None = 0,
    MaxAttemptsReached = 1,
    InvalidHandleError = 2,
    RetryableError = 3
}

internal readonly record struct WindowTopmostInteropRetryDecision(
    bool ShouldRetry,
    WindowTopmostInteropRetryReason Reason);

internal static class WindowTopmostInteropRetryPolicy
{
    internal static WindowTopmostInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        var coreDecision = WindowInteropRetryPolicyCore.Resolve(attempt, errorCode);
        return coreDecision switch
        {
            WindowInteropRetryCoreDecision.MaxAttemptsReached => new WindowTopmostInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowTopmostInteropRetryReason.MaxAttemptsReached),
            WindowInteropRetryCoreDecision.InvalidHandleError => new WindowTopmostInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowTopmostInteropRetryReason.InvalidHandleError),
            _ => new WindowTopmostInteropRetryDecision(
                ShouldRetry: true,
                Reason: WindowTopmostInteropRetryReason.RetryableError)
        };
    }

    internal static bool ShouldRetry(int attempt, int errorCode)
    {
        return Resolve(attempt, errorCode).ShouldRetry;
    }
}
