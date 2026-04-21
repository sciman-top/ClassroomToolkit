namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostWatchdogPolicy
{
    private const int IntervalMs = 700;

    internal static int ResolveIntervalMs() => IntervalMs;

    internal static bool ShouldForceRetouch(
        bool toolbarVisible,
        bool rollCallVisible,
        bool launcherVisible,
        bool imageManagerVisible,
        bool rollCallAuxOverlayVisible,
        bool photoModeActive)
    {
        if (photoModeActive)
        {
            return false;
        }

        return toolbarVisible
            || rollCallVisible
            || launcherVisible
            || imageManagerVisible
            || rollCallAuxOverlayVisible;
    }
}
