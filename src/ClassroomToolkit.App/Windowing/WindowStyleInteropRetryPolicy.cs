namespace ClassroomToolkit.App.Windowing;

internal enum WindowStyleInteropRetryReason
{
    None = 0,
    MaxAttemptsReached = 1,
    InvalidHandleError = 2,
    RetryableError = 3
}

internal readonly record struct WindowStyleInteropRetryDecision(
    bool ShouldRetry,
    WindowStyleInteropRetryReason Reason);

internal static class WindowStyleInteropRetryPolicy
{
    internal static WindowStyleInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        var coreDecision = WindowInteropRetryPolicyCore.Resolve(attempt, errorCode);
        return coreDecision switch
        {
            WindowInteropRetryCoreDecision.MaxAttemptsReached => new WindowStyleInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowStyleInteropRetryReason.MaxAttemptsReached),
            WindowInteropRetryCoreDecision.InvalidHandleError => new WindowStyleInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowStyleInteropRetryReason.InvalidHandleError),
            _ => new WindowStyleInteropRetryDecision(
                ShouldRetry: true,
                Reason: WindowStyleInteropRetryReason.RetryableError)
        };
    }

    internal static bool ShouldRetry(int attempt, int errorCode)
    {
        return Resolve(attempt, errorCode).ShouldRetry;
    }
}
