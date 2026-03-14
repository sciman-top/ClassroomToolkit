using System;

namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborPagesClearPolicy
{
    internal static bool ShouldKeepFrames(
        bool hasVisibleNeighborFrame,
        bool interactionActive,
        DateTime lastNonEmptyUtc,
        DateTime nowUtc,
        int clearGraceMs)
    {
        if (!hasVisibleNeighborFrame)
        {
            return false;
        }

        if (interactionActive)
        {
            return true;
        }

        if (lastNonEmptyUtc == CrossPageRuntimeDefaults.UnsetTimestampUtc
            || clearGraceMs <= CrossPageNeighborPagesClearDefaults.MinGraceMs)
        {
            return false;
        }

        return (nowUtc - lastNonEmptyUtc).TotalMilliseconds < clearGraceMs;
    }
}
