namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborHeightResolvePolicy
{
    internal static bool ShouldAllowSynchronousResolve(
        bool interactionActive,
        bool photoDocumentIsPdf)
    {
        // For image sequences, synchronous bitmap loads during active interaction
        // can block UI input handling. Prefer cache/fallback heights.
        if (interactionActive && !photoDocumentIsPdf)
        {
            return false;
        }

        return true;
    }
}
