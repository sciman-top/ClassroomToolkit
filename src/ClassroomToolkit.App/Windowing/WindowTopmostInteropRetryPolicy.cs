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
    private const int MaxRetryAttempts = WindowInteropRetryDefaults.MaxRetryAttempts;
    private const int ErrorInvalidWindowHandle = WindowInteropRetryDefaults.ErrorInvalidWindowHandle;
    private const int ErrorInvalidHandle = WindowInteropRetryDefaults.ErrorInvalidHandle;

    internal static WindowTopmostInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        if (attempt >= MaxRetryAttempts)
        {
            return new WindowTopmostInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowTopmostInteropRetryReason.MaxAttemptsReached);
        }

        // Invalid handle errors are non-recoverable for current operation.
        if (errorCode is ErrorInvalidWindowHandle or ErrorInvalidHandle)
        {
            return new WindowTopmostInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowTopmostInteropRetryReason.InvalidHandleError);
        }

        // For other errors, perform bounded retry because z-order races are possible
        // when windows are rapidly shown/hidden.
        return new WindowTopmostInteropRetryDecision(
            ShouldRetry: true,
            Reason: WindowTopmostInteropRetryReason.RetryableError);
    }

    internal static bool ShouldRetry(int attempt, int errorCode)
    {
        return Resolve(attempt, errorCode).ShouldRetry;
    }
}
