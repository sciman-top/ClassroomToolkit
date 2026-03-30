namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInteractiveInkClearPolicy
{
    internal static bool ShouldClearCurrentFrame(
        bool holdInkReplacement,
        bool hasNeighborInkStrokes,
        bool inkOperationActive,
        bool interactionActive)
    {
        return !holdInkReplacement
            && !interactionActive
            && !inkOperationActive
            && !hasNeighborInkStrokes;
    }
}
