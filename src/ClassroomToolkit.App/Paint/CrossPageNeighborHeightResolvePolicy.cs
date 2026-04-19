namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborHeightResolvePolicy
{
    internal static bool ShouldAllowSynchronousResolve(
        bool interactionActive,
        bool photoDocumentIsPdf)
    {
        if (!interactionActive)
        {
            return true;
        }

        // PDF page-size probing is cheap and keeps seam bounds stable during zoom/pan.
        // Keep image sequence interaction on async path to avoid decode stalls.
        return photoDocumentIsPdf;
    }
}
