using System;

namespace ClassroomToolkit.App;

internal static class RollCallClickSuppressionPolicy
{
    internal static DateTime ExtendSuppressUntil(
        DateTime currentSuppressUntilUtc,
        DateTime nowUtc,
        TimeSpan duration)
    {
        if (duration <= TimeSpan.Zero)
        {
            return currentSuppressUntilUtc;
        }

        var nextSuppressUntilUtc = nowUtc.Add(duration);
        return nextSuppressUntilUtc > currentSuppressUntilUtc
            ? nextSuppressUntilUtc
            : currentSuppressUntilUtc;
    }

    internal static bool ShouldSuppress(
        DateTime suppressUntilUtc,
        DateTime nowUtc)
    {
        return suppressUntilUtc >= nowUtc;
    }
}
