namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveNeighborInkHoldPolicy
{
    internal static bool Resolve(
        bool baseHoldReplacement,
        bool interactionActive,
        bool hasCurrentInkFrame,
        bool inkOperationActive,
        bool slotPageChanged)
    {
        // Slot remap means this slot now points to another page. Holding current frame here
        // keeps old-page ink alive on the wrong slot and manifests as seam flash/jitter.
        if (slotPageChanged)
        {
            return false;
        }

        if (baseHoldReplacement)
        {
            return true;
        }

        // Keep current neighbor ink only while interactive ink mutation is in progress.
        // Plain cursor pan should not hold stale frames, otherwise old-page ink can
        // temporarily ride on top of a remapped neighbor page.
        return interactionActive && inkOperationActive && hasCurrentInkFrame;
    }
}
