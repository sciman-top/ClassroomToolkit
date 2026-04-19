namespace ClassroomToolkit.App.Photos;

internal static class ImageManagerActivationPolicy
{
    internal static bool ShouldOpenOnSingleClick(bool isFolder, bool isPdf, bool isImage)
    {
        return false;
    }

    internal static bool ShouldOpenOnDoubleClick(bool isFolder, bool isPdf, bool isImage)
    {
        return isFolder || isPdf || isImage;
    }
}
