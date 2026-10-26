
namespace ClassroomToolkit.App.Windowing;

internal static class WindowInteropRetryDefaults
{
    internal const int MaxRetryAttempts = 2;
    internal const int ErrorInvalidWindowHandle = 1400;
    internal const int ErrorInvalidHandle = 6;
}

internal enum WindowInteropRetryReason
{
    None = 0,
    MaxAttemptsReached = 1,
    InvalidHandleError = 2,
    RetryableError = 3
}

internal readonly record struct WindowInteropRetryDecision(
    bool ShouldRetry,
    WindowInteropRetryReason Reason);

internal static class WindowInteropRetryPolicies
{
    internal static WindowInteropRetryDecision Resolve(int attempt, int errorCode)
    {
        if (attempt >= WindowInteropRetryDefaults.MaxRetryAttempts)
        {
            return new WindowInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowInteropRetryReason.MaxAttemptsReached);
        }

        if (errorCode is WindowInteropRetryDefaults.ErrorInvalidWindowHandle
            or WindowInteropRetryDefaults.ErrorInvalidHandle)
        {
            return new WindowInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowInteropRetryReason.InvalidHandleError);
        }

        return new WindowInteropRetryDecision(
            ShouldRetry: true,
            Reason: WindowInteropRetryReason.RetryableError);
    }
}
