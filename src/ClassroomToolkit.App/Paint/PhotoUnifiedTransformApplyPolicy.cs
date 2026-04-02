namespace ClassroomToolkit.App.Paint;

internal static class PhotoUnifiedTransformApplyPolicy
{
    internal static bool ShouldApplyRuntimeTransform(
        bool rememberPhotoTransform,
        bool photoInkModeActive,
        bool crossPageDisplayActive)
    {
        return rememberPhotoTransform
            && photoInkModeActive
            && crossPageDisplayActive;
    }
}
