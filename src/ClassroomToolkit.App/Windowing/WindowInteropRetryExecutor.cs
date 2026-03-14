using System;
using System.Threading;

namespace ClassroomToolkit.App.Windowing;

internal static class WindowInteropRetryExecutor
{
    internal static bool Execute(Func<int, (bool Success, int ErrorCode)> attemptAction, Func<int, int, bool> shouldRetry)
    {
        ArgumentNullException.ThrowIfNull(attemptAction);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        for (var attempt = 1; ; attempt++)
        {
            var result = attemptAction(attempt);
            if (result.Success)
            {
                return true;
            }

            if (!shouldRetry(attempt, result.ErrorCode))
            {
                return false;
            }

            Thread.Sleep(WindowInteropRuntimeDefaults.RetrySleepMs);
        }
    }

    internal static bool ExecuteWithValue<T>(
        Func<int, (bool Success, T Value, int ErrorCode)> attemptAction,
        Func<int, int, bool> shouldRetry,
        out T value)
    {
        ArgumentNullException.ThrowIfNull(attemptAction);
        ArgumentNullException.ThrowIfNull(shouldRetry);

        value = default!;
        for (var attempt = 1; ; attempt++)
        {
            var result = attemptAction(attempt);
            if (result.Success)
            {
                value = result.Value;
                return true;
            }

            if (!shouldRetry(attempt, result.ErrorCode))
            {
                return false;
            }

            Thread.Sleep(WindowInteropRuntimeDefaults.RetrySleepMs);
        }
    }
}
