namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInputDisplayPolicy
{
    internal static bool IsActive(
        bool photoModeActive,
        bool boardActive,
        bool crossPageDisplayEnabled)
    {
        return PhotoInteractionModePolicy.IsCrossPageDisplayActive(
            photoModeActive,
            boardActive,
            crossPageDisplayEnabled);
    }
}
