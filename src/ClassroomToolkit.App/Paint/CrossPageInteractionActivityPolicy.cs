namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractionActivityPolicy
{
    internal static bool IsActive(
        bool photoPanning,
        bool crossPageDragging,
        bool inkOperationActive)
    {
        return photoPanning || crossPageDragging || inkOperationActive;
    }
}
