using System;

namespace ClassroomToolkit.App;

internal static class LauncherTopmostVisibilityTimestampPolicy
{
    internal static DateTime ResolveLastVisibleUtc(
        DateTime previousUtc,
        DateTime nowUtc,
        bool visibleForTopmost)
    {
        return visibleForTopmost ? nowUtc : previousUtc;
    }
}
