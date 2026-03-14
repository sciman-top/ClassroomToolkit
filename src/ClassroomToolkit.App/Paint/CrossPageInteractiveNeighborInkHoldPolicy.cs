namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveNeighborInkHoldPolicy
{
    internal static bool Resolve(
        bool baseHoldReplacement,
        bool interactionActive,
        bool hasCurrentInkFrame,
        bool inkOperationActive)
    {
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
