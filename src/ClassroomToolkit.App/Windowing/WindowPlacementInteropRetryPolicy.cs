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
    private const int MaxRetryAttempts = WindowInteropRetryDefaults.MaxRetryAttempts;
    private const int ErrorInvalidWindowHandle = WindowInteropRetryDefaults.ErrorInvalidWindowHandle;
    private const int ErrorInvalidHandle = WindowInteropRetryDefaults.ErrorInvalidHandle;

    internal static WindowPlacementInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        if (attempt >= MaxRetryAttempts)
        {
            return new WindowPlacementInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowPlacementInteropRetryReason.MaxAttemptsReached);
        }

        if (errorCode is ErrorInvalidWindowHandle or ErrorInvalidHandle)
        {
            return new WindowPlacementInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowPlacementInteropRetryReason.InvalidHandleError);
        }

        return new WindowPlacementInteropRetryDecision(
            ShouldRetry: true,
            Reason: WindowPlacementInteropRetryReason.RetryableError);
    }

    internal static bool ShouldRetry(int attempt, int errorCode)
    {
        return Resolve(attempt, errorCode).ShouldRetry;
    }
}
