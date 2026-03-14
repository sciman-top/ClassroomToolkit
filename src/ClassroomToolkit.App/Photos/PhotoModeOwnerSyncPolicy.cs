namespace ClassroomToolkit.App.Photos;

internal static class PhotoModeOwnerSyncPolicy
{
    internal static bool ShouldSyncOwners(bool touchPhotoFullscreenSurface)
    {
        return !touchPhotoFullscreenSurface;
    }
}
