namespace ClassroomToolkit.App.Paint;

internal enum PhotoLoadedBitmapTransformPath
{
    ApplyUnifiedTransform = 0,
    FitToViewport = 1,
    TryStoredTransformThenFit = 2
}

internal static class PhotoLoadedBitmapTransformPathPolicy
{
    internal static PhotoLoadedBitmapTransformPath Resolve(
        bool useCrossPageUnifiedPath,
        bool rememberPhotoTransform,
        bool photoUnifiedTransformReady)
    {
        if (!rememberPhotoTransform)
        {
            return PhotoLoadedBitmapTransformPath.FitToViewport;
        }

        if (!useCrossPageUnifiedPath)
        {
            return PhotoLoadedBitmapTransformPath.TryStoredTransformThenFit;
        }

        return photoUnifiedTransformReady
            ? PhotoLoadedBitmapTransformPath.ApplyUnifiedTransform
            : PhotoLoadedBitmapTransformPath.FitToViewport;
    }
}
