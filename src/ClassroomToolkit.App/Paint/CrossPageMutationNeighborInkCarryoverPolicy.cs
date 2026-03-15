namespace ClassroomToolkit.App.Paint;

internal static class CrossPageMutationNeighborInkCarryoverPolicy
{
    internal static bool ShouldClearPreservedNeighborInkFrames(
        bool pageChanged,
        bool interactiveSwitch,
        bool inputTriggeredByActiveInkMutation,
        PaintToolMode mode)
    {
        if (!pageChanged || interactiveSwitch || !inputTriggeredByActiveInkMutation)
        {
            return false;
        }

        return mode == PaintToolMode.Brush;
    }
}
