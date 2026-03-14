using System;

namespace ClassroomToolkit.App;

internal static class LauncherTopmostVisibilityStateUpdater
{
    internal static void ApplyResolvedTimestamp(
        ref DateTime lastVisibleUtc,
        DateTime nowUtc,
        bool visibleForTopmost)
    {
        lastVisibleUtc = LauncherTopmostVisibilityTimestampPolicy.ResolveLastVisibleUtc(
            lastVisibleUtc,
            nowUtc,
            visibleForTopmost);
    }
}
