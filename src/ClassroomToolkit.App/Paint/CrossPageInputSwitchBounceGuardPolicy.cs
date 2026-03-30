using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputSwitchBounceGuardPolicy
{
    internal static bool ShouldSuppress(
        int currentPage,
        int targetPage,
        int lastSwitchFromPage,
        int lastSwitchToPage,
        DateTime lastSwitchUtc,
        DateTime nowUtc,
        double pointerY,
        double seamY,
        double seamBandDip,
        int cooldownMs)
    {
        if (currentPage <= 0
            || targetPage <= 0
            || currentPage == targetPage
            || lastSwitchUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        var reverseDirection = lastSwitchFromPage == targetPage
            && lastSwitchToPage == currentPage;
        if (!reverseDirection)
        {
            return false;
        }

        var elapsedMs = (nowUtc - lastSwitchUtc).TotalMilliseconds;
        if (elapsedMs < 0 || elapsedMs > cooldownMs)
        {
            return false;
        }

        return Math.Abs(pointerY - seamY) <= seamBandDip;
    }
}
