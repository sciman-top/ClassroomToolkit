namespace ClassroomToolkit.App.Windowing;

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

