using System;

namespace ClassroomToolkit.App.Photos;

internal static class StudentPhotoCachePolicy
{
    internal static bool ShouldReuseCache(
        DateTime nowUtc,
        DateTime cachedUtc,
        TimeSpan ttl)
    {
        return nowUtc - cachedUtc < ttl;
    }

    internal static bool ShouldSkipMissProbe(
        DateTime nowUtc,
        DateTime lastMissProbeUtc,
        TimeSpan probeInterval)
    {
        if (lastMissProbeUtc == DateTime.MinValue)
        {
            return false;
        }

        return nowUtc - lastMissProbeUtc < probeInterval;
    }
}
