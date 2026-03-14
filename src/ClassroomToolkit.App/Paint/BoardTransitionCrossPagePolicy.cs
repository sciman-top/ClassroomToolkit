namespace ClassroomToolkit.App.Paint;

internal static class BoardTransitionCrossPagePolicy
{
    internal static bool ShouldHandleCrossPageArtifacts(
        bool photoModeActive,
        bool crossPageDisplayEnabled)
    {
        // Board transitions need to clear/refresh cross-page artifacts when photo mode
        // is the active scene source, regardless of the new board active flag.
        return photoModeActive && crossPageDisplayEnabled;
    }
}
