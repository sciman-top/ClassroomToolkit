namespace ClassroomToolkit.App.Photos;

internal static class ImageManagerDiagnosticsPolicy
{
    internal static string FormatFavoriteFolderDialogFailureMessage(string message)
    {
        return $"[ImageManager] favorite-folder-dialog-failed msg={message}";
    }

    internal static string FormatUpNavigationFailureMessage(string folder, string message)
    {
        return $"[ImageManager] up-navigation-failed folder={folder} msg={message}";
    }

    internal static string FormatThumbnailDispatchFailureMessage(string path, string message)
    {
        return $"[ImageManager] thumbnail-dispatch-failed path={path} msg={message}";
    }

    internal static string FormatFolderExpandFailureMessage(string path, string message)
    {
        return $"[ImageManager] folder-expand-failed path={path} msg={message}";
    }
}
