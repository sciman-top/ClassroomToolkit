namespace ClassroomToolkit.App.Paint;

internal static class PresentationFocusMonitorPolicy
{
    internal static bool ShouldAttemptRestore(
        bool restoreEnabled,
        bool photoModeActive,
        bool boardActive,
        bool foregroundOwnedByCurrentProcess,
        DateTime nowUtc,
        DateTime nextAttemptUtc)
    {
        if (!restoreEnabled || photoModeActive || boardActive)
        {
            return false;
        }

        if (nowUtc < nextAttemptUtc)
        {
            return false;
        }

        return foregroundOwnedByCurrentProcess;
    }

    internal static DateTime ComputeNextAttemptUtc(DateTime nowUtc, int cooldownMs)
    {
        return nowUtc.AddMilliseconds(cooldownMs);
    }
}
