namespace ClassroomToolkit.App.Paint;

internal static class PhotoTransformMemoryTogglePolicy
{
    internal static bool ShouldResetUserDirtyState(bool rememberPhotoTransform)
    {
        return !rememberPhotoTransform;
    }

    internal static bool ShouldResetUnifiedTransformState(bool rememberPhotoTransform)
    {
        return !rememberPhotoTransform;
    }
}
