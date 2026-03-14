namespace ClassroomToolkit.App.Paint;

internal static class CrossPageNeighborPrefetchGatePolicy
{
    internal static bool ShouldSchedule(
        bool photoModeActive,
        bool photoDocumentIsPdf,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        if (!photoModeActive || photoDocumentIsPdf)
        {
            return false;
        }

        if (!crossPageDisplayEnabled && interactionActive)
        {
            return false;
        }

        return true;
    }

    internal static bool ShouldRunPrefetch(
        bool photoModeActive,
        bool photoDocumentIsPdf,
        bool crossPageDisplayEnabled,
        bool interactionActive)
    {
        return photoModeActive
            && !photoDocumentIsPdf
            && crossPageDisplayEnabled
            && !interactionActive;
    }
}
