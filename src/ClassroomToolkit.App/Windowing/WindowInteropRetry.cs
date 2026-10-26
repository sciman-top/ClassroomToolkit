
namespace ClassroomToolkit.App.Windowing;

internal static class WindowInteropRetryDefaults
{
    internal const int MaxRetryAttempts = 2;
    internal const int ErrorInvalidWindowHandle = 1400;
    internal const int ErrorInvalidHandle = 6;
}

internal enum WindowInteropRetryCoreDecision
{
    Retryable = 0,
    MaxAttemptsReached = 1,
    InvalidHandleError = 2
}

internal static class WindowInteropRetryPolicyCore
{
    private const int MaxRetryAttempts = WindowInteropRetryDefaults.MaxRetryAttempts;
    private const int ErrorInvalidWindowHandle = WindowInteropRetryDefaults.ErrorInvalidWindowHandle;
    private const int ErrorInvalidHandle = WindowInteropRetryDefaults.ErrorInvalidHandle;

    internal static WindowInteropRetryCoreDecision Resolve(int attempt, int errorCode)
    {
        if (attempt >= MaxRetryAttempts)
        {
            return WindowInteropRetryCoreDecision.MaxAttemptsReached;
        }

        if (errorCode is ErrorInvalidWindowHandle or ErrorInvalidHandle)
        {
            return WindowInteropRetryCoreDecision.InvalidHandleError;
        }

        return WindowInteropRetryCoreDecision.Retryable;
    }
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
        return WindowInteropRetryPolicyCore.Resolve(attempt, errorCode) switch
        {
            WindowInteropRetryCoreDecision.MaxAttemptsReached => new WindowInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowInteropRetryReason.MaxAttemptsReached),
            WindowInteropRetryCoreDecision.InvalidHandleError => new WindowInteropRetryDecision(
                ShouldRetry: false,
                Reason: WindowInteropRetryReason.InvalidHandleError),
            _ => new WindowInteropRetryDecision(
                ShouldRetry: true,
                Reason: WindowInteropRetryReason.RetryableError)
        };
    }
}
