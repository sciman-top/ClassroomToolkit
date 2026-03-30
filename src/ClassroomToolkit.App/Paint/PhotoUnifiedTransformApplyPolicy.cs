namespace ClassroomToolkit.App.Paint;

internal static class PhotoUnifiedTransformApplyPolicy
{
    internal static bool ShouldApplyRuntimeTransform(
        bool photoInkModeActive,
        bool crossPageDisplayActive)
    {
        return photoInkModeActive && crossPageDisplayActive;
    }
}
