using System;
using System.Threading;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowInteropRetryExecutor
{
    internal static bool Execute(
        Func<int, (bool Success, int ErrorCode)> attemptAction,
        Func<int, int, bool> shouldRetry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attemptAction);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        var succeeded = ExecuteCore(
            attempt => ResolveAttempt(attemptAction, attempt),
            shouldRetry,
            cancellationToken,
            out _);
        return succeeded;
    }

    internal static bool ExecuteWithValue<T>(
        Func<int, (bool Success, T Value, int ErrorCode)> attemptAction,
        Func<int, int, bool> shouldRetry,
        out T value,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attemptAction);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        return ExecuteCore(
            attempt => ResolveAttempt(attemptAction, attempt),
            shouldRetry,
            cancellationToken,
            out value);
    }

    private static bool ExecuteCore<TValue>(
        Func<int, (bool Invoked, bool Success, TValue Value, int ErrorCode)> attempt,
        Func<int, int, bool> shouldRetry,
        CancellationToken cancellationToken,
        out TValue value)
    {
        value = default!;

        for (var retryAttempt = 1; ; retryAttempt++)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return false;
            }

            var result = SafeActionExecutionExecutor.TryExecute(
                () => attempt(retryAttempt),
                fallback: (Invoked: false, Success: false, Value: default!, ErrorCode: 0));
            if (!result.Invoked)
            {
                return false;
            }

            if (result.Success)
            {
                value = result.Value;
                return true;
            }

            var retry = SafeActionExecutionExecutor.TryExecute(
                () => shouldRetry(retryAttempt, result.ErrorCode),
                fallback: false);

            if (!retry)
            {
                return false;
            }

            var retrySleepMs = WindowInteropRuntimeDefaults.RetrySleepMs;
            if (retrySleepMs > 0)
            {
                if (!WaitBeforeRetry(retrySleepMs, cancellationToken))
                {
                    return false;
                }
            }
        }
    }

    private static bool WaitBeforeRetry(int retrySleepMs, CancellationToken cancellationToken)
    {
        try
        {
            return !cancellationToken.WaitHandle.WaitOne(retrySleepMs);
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private static (bool Invoked, bool Success, bool Value, int ErrorCode) ResolveAttempt(
        Func<int, (bool Success, int ErrorCode)> attemptAction,
        int attempt)
    {
        var attemptResult = attemptAction(attempt);
        return (Invoked: true, attemptResult.Success, Value: attemptResult.Success, attemptResult.ErrorCode);
    }

    private static (bool Invoked, bool Success, TValue Value, int ErrorCode) ResolveAttempt<TValue>(
        Func<int, (bool Success, TValue Value, int ErrorCode)> attemptAction,
        int attempt)
    {
        var attemptResult = attemptAction(attempt);
        return (Invoked: true, attemptResult.Success, attemptResult.Value, attemptResult.ErrorCode);
    }
}
