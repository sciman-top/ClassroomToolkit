namespace ClassroomToolkit.App.Paint;

internal static class PhotoPanEndRedrawPolicy
{
    internal static bool ShouldRequestInkRedraw(
        bool hadEffectiveMovement,
        bool hadCrossPageDragCommit)
    {
        return hadEffectiveMovement || hadCrossPageDragCommit;
    }
}
