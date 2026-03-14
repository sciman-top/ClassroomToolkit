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
    private const int MaxRetryAttempts = WindowInteropRetryDefaults.MaxRetryAttempts;
    private const int ErrorInvalidWindowHandle = WindowInteropRetryDefaults.ErrorInvalidWindowHandle;
    private const int ErrorInvalidHandle = WindowInteropRetryDefaults.ErrorInvalidHandle;

    internal static WindowStyleInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        if (attempt >= MaxRetryAttempts)
        {
            return new WindowStyleInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowStyleInteropRetryReason.MaxAttemptsReached);
        }

        if (errorCode is ErrorInvalidWindowHandle or ErrorInvalidHandle)
        {
            return new WindowStyleInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowStyleInteropRetryReason.InvalidHandleError);
        }

        return new WindowStyleInteropRetryDecision(
            ShouldRetry: true,
            Reason: WindowStyleInteropRetryReason.RetryableError);
    }

    internal static bool ShouldRetry(int attempt, int errorCode)
    {
        return Resolve(attempt, errorCode).ShouldRetry;
    }
}
