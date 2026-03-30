using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveInkFrameHoldPolicy
{
    internal static bool ShouldHoldReplacement(
        int pageIndex,
        int pinnedNeighborPage,
        DateTime holdUntilUtc,
        DateTime nowUtc,
        bool hasCurrentInkFrame)
    {
        if (!hasCurrentInkFrame)
        {
            return false;
        }
        if (pinnedNeighborPage <= 0 || pageIndex != pinnedNeighborPage)
        {
            return false;
        }
        if (holdUntilUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            return false;
        }

        return nowUtc <= holdUntilUtc;
    }
}
