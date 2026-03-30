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
}
