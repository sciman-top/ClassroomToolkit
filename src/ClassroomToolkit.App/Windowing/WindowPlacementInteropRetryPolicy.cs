namespace ClassroomToolkit.App.Windowing;

internal enum WindowPlacementInteropRetryReason
{
    None = 0,
    MaxAttemptsReached = 1,
    InvalidHandleError = 2,
    RetryableError = 3
}

internal readonly record struct WindowPlacementInteropRetryDecision(
    bool ShouldRetry,
    WindowPlacementInteropRetryReason Reason);

internal static class WindowPlacementInteropRetryPolicy
{
    internal static WindowPlacementInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        var coreDecision = WindowInteropRetryPolicyCore.Resolve(attempt, errorCode);
        return coreDecision switch
        {
            WindowInteropRetryCoreDecision.MaxAttemptsReached => new WindowPlacementInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowPlacementInteropRetryReason.MaxAttemptsReached),
            WindowInteropRetryCoreDecision.InvalidHandleError => new WindowPlacementInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowPlacementInteropRetryReason.InvalidHandleError),
            _ => new WindowPlacementInteropRetryDecision(
                ShouldRetry: true,
                Reason: WindowPlacementInteropRetryReason.RetryableError)
        };
    }

    internal static bool ShouldRetry(int attempt, int errorCode)
    {
        return Resolve(attempt, errorCode).ShouldRetry;
    }
}
