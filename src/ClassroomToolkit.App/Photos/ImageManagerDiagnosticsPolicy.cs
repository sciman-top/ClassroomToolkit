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

    internal static string FormatFileAttributeReadFailureMessage(string path, string exceptionType, string message)
    {
        return $"[ImageManager] file-attribute-read-failed path={path} ex={exceptionType} msg={message}";
    }

    internal static string FormatThumbnailLoadFailureMessage(string path, string sourceType, string exceptionType, string message)
    {
        return $"[ImageManager] thumbnail-load-failed path={path} source={sourceType} ex={exceptionType} msg={message}";
    }

    internal static string FormatPdfMetadataReadFailureMessage(string path, string exceptionType, string message)
    {
        return $"[ImageManager] pdf-metadata-read-failed path={path} ex={exceptionType} msg={message}";
    }

    internal static string FormatModifiedTimeReadFailureMessage(string path, string exceptionType, string message)
    {
        return $"[ImageManager] modified-time-read-failed path={path} ex={exceptionType} msg={message}";
    }
}
