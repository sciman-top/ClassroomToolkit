using ClassroomToolkit.App.Ink;
using System.Collections.Generic;
using System.Windows;
using System;

namespace ClassroomToolkit.App.Photos;

public interface IImageManagerWindowFactory
{
    ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents);
}

internal static class ImageManagerOpenSurfaceApplyPolicy
{
    internal static bool ShouldApply(bool touchImageManagerSurface, bool requestZOrderApply)
    {
        return touchImageManagerSurface || requestZOrderApply;
    }
}

internal static class ImageManagerStateChangeSurfaceApplyPolicy
{
    internal static bool ShouldApply(bool requestZOrderApply, bool forceEnforceZOrder)
    {
        return requestZOrderApply || forceEnforceZOrder;
    }
}

internal static class ImageManagerActivationPolicy
{
    internal static bool ShouldOpenOnSingleClick(bool isFolder, bool isPdf, bool isImage)
    {
        return isFolder || isPdf || isImage;
    }

    internal static bool ShouldOpenOnDoubleClick(bool isFolder, bool isPdf, bool isImage)
    {
        return isFolder || isPdf || isImage;
    }
}

internal sealed class ImageManagerWindowFactory : IImageManagerWindowFactory
{
    private readonly InkPersistenceService _persistence;
    private readonly InkExportService _export;

    public ImageManagerWindowFactory(InkPersistenceService persistence, InkExportService export)
    {
        _persistence = persistence;
        _export = export;
    }

    public ImageManagerWindow Create(IReadOnlyList<string> favorites, IReadOnlyList<string> recents)
    {
        var window = new ImageManagerWindow(favorites, recents);
        window.SetInkPersistenceService(_persistence, _export);
        return window;
    }
}

internal readonly record struct ImageManagerInkCleanupSummary(
    int SidecarsDeleted,
    int CompositesDeleted);

internal static class ImageManagerInkCleanupExecutor
{
    internal static ImageManagerInkCleanupSummary Cleanup(
        string folder,
        Func<string, int> cleanupSidecars,
        Func<string, int> cleanupComposites)
    {
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new ImageManagerInkCleanupSummary(0, 0);
        }

        return new ImageManagerInkCleanupSummary(
            SidecarsDeleted: cleanupSidecars(folder),
            CompositesDeleted: cleanupComposites(folder));
    }
}

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

internal readonly record struct ImageManagerRestoreBoundsPlan(
    double Width,
    double Height,
    double Left,
    double Top);

internal static class ImageManagerRestoreBoundsPolicy
{
    internal const double WorkAreaWidthRatio = 0.92;
    internal const double WorkAreaHeightRatio = 0.90;

    internal static ImageManagerRestoreBoundsPlan Resolve(
        double restoredWidth,
        double restoredHeight,
        double defaultWidth,
        double defaultHeight,
        double minWidth,
        double minHeight,
        Rect workArea)
    {
        var safeDefaultWidth = Math.Max(1, defaultWidth);
        var safeDefaultHeight = Math.Max(1, defaultHeight);
        var safeMinWidth = Math.Max(1, minWidth);
        var safeMinHeight = Math.Max(1, minHeight);

        // Restore should return to a manageable, teaching-friendly window size.
        // Keep smaller user-resized bounds, but clamp oversized history to default size.
        var maxRestoreWidth = Math.Max(safeMinWidth, safeDefaultWidth);
        var maxRestoreHeight = Math.Max(safeMinHeight, safeDefaultHeight);
        var desiredWidth = Math.Clamp(restoredWidth, safeMinWidth, maxRestoreWidth);
        var desiredHeight = Math.Clamp(restoredHeight, safeMinHeight, maxRestoreHeight);

        if (workArea.Width > 0 && workArea.Height > 0)
        {
            var maxWidth = Math.Max(safeMinWidth, workArea.Width * WorkAreaWidthRatio);
            var maxHeight = Math.Max(safeMinHeight, workArea.Height * WorkAreaHeightRatio);
            desiredWidth = Math.Clamp(desiredWidth, safeMinWidth, maxWidth);
            desiredHeight = Math.Clamp(desiredHeight, safeMinHeight, maxHeight);

            var left = workArea.Left + (workArea.Width - desiredWidth) * 0.5;
            var top = workArea.Top + (workArea.Height - desiredHeight) * 0.5;
            return new ImageManagerRestoreBoundsPlan(
                Width: desiredWidth,
                Height: desiredHeight,
                Left: left,
                Top: top);
        }

        return new ImageManagerRestoreBoundsPlan(
            Width: Math.Max(safeMinWidth, desiredWidth),
            Height: Math.Max(safeMinHeight, desiredHeight),
            Left: double.NaN,
            Top: double.NaN);
    }
}
