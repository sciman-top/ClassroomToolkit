namespace ClassroomToolkit.App.Paint;

internal static class CrossPageCurrentPagePointerHitPolicy
{
    internal static bool ShouldUseCurrentPage(
        bool hasCurrentBitmap,
        bool hasCurrentRect,
        bool pointerInsideCurrentRect)
    {
        return hasCurrentBitmap && hasCurrentRect && pointerInsideCurrentRect;
    }
}
