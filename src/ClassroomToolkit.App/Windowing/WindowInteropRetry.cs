
namespace ClassroomToolkit.App.Windowing;

internal static class WindowInteropRetryDefaults
{
    internal const int MaxRetryAttempts = 2;
    internal const int ErrorInvalidWindowHandle = 1400;
    internal const int ErrorInvalidHandle = 6;
}

internal static class WindowInteropRuntimeDefaults
{
    internal const int RetrySleepMs = 0;
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

internal static class WindowInteropRetryPolicies
{
    internal static WindowPlacementInteropRetryDecision ResolveWindowPlacementInteropRetry(int attempt, int errorCode)
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

    internal static bool ShouldRetryWindowPlacementInteropRetry(int attempt, int errorCode)
    {
        return ResolveWindowPlacementInteropRetry(attempt, errorCode).ShouldRetry;
    }

    internal static WindowStyleInteropRetryDecision ResolveWindowStyleInteropRetry(int attempt, int errorCode)
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

    internal static bool ShouldRetryWindowStyleInteropRetry(int attempt, int errorCode)
    {
        return ResolveWindowStyleInteropRetry(attempt, errorCode).ShouldRetry;
    }

    internal static WindowTopmostInteropRetryDecision ResolveWindowTopmostInteropRetry(int attempt, int errorCode)
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

    internal static bool ShouldRetryWindowTopmostInteropRetry(int attempt, int errorCode)
    {
        return ResolveWindowTopmostInteropRetry(attempt, errorCode).ShouldRetry;
    }
}
