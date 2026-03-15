namespace ClassroomToolkit.App.Paint;

internal static class CrossPageCurrentPageSeedSlotHidePolicy
{
    internal static bool ShouldHide(PaintToolMode mode)
    {
        return mode != PaintToolMode.Brush;
    }
}
