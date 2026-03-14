namespace ClassroomToolkit.App.Windowing;

internal static class LauncherTopmostVisibilityHoldPolicy
{
    internal static bool ResolveVisibleForRepair(
        bool currentVisibleForTopmost,
        DateTime lastVisibleForTopmostUtc,
        DateTime nowUtc,
        int holdMs = LauncherTopmostVisibilityHoldDefaults.HoldMs)
    {
        if (currentVisibleForTopmost)
        {
            return true;
        }

        if (lastVisibleForTopmostUtc == MainWindowRuntimeDefaults.DefaultTimestampUtc)
        {
            return false;
        }

        var elapsedMs = (nowUtc - lastVisibleForTopmostUtc).TotalMilliseconds;
        return elapsedMs >= 0 && elapsedMs <= holdMs;
    }
}
