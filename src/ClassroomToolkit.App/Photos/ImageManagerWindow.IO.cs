using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
    private enum MediaFileKind
    {
        None = 0,
        Pdf = 1,
        Image = 2
    }

    private static readonly EnumerationOptions TopLevelIgnoreInaccessibleOptions = new()
    {
        RecurseSubdirectories = false,
        IgnoreInaccessible = true
    };

    private static bool IsPdfFile(string path)
    {
        return GetMediaFileKind(path.AsSpan()) == MediaFileKind.Pdf;
    }

    private static MediaFileKind GetMediaFileKind(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return MediaFileKind.None;
        }

        var dotIndex = text.LastIndexOf('.');
        if (dotIndex < 0 || dotIndex >= text.Length - 1)
        {
            return MediaFileKind.None;
        }

        var extension = text[dotIndex..];
        if (extension.Equals(".pdf".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MediaFileKind.Pdf;
        }

        if (extension.Equals(".png".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".webp".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            return MediaFileKind.Image;
        }

        return MediaFileKind.None;
    }

    private static bool IsHiddenFile(string path)
    {
        return ExecuteIoSafe(
            () =>
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        },
            fallback: false,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatFileAttributeReadFailureMessage(
                    path,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static ImageSource? LoadThumbnail(string path, int decodePixelWidth)
    {
        return ExecuteIoSafe<ImageSource?>(
            () =>
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        },
            fallback: null,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatThumbnailLoadFailureMessage(
                    path,
                    sourceType: "image",
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static (ImageSource? Thumbnail, int PageCount) LoadPdfPreview(string path)
    {
        return ExecuteIoSafe(
            () =>
        {
            using var doc = PdfDocumentHost.Open(path);
            return (doc.RenderPage(1, 96), doc.PageCount);
        },
            fallback: (null, 0),
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatThumbnailLoadFailureMessage(
                    path,
                    sourceType: "pdf",
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static DateTime GetModifiedTime(string path)
    {
        return ExecuteIoSafe(
            () => File.GetLastWriteTime(path),
            fallback: DateTime.MinValue,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerDiagnosticsPolicy.FormatModifiedTimeReadFailureMessage(
                    path,
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static void ExecuteIoSafe(Action action, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            action();
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            onFailure(ex);
        }
    }

    private static TResult ExecuteIoSafe<TResult>(
        Func<TResult> action,
        TResult fallback,
        Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            return action();
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            onFailure(ex);
            return fallback;
        }
    }

    private List<ImageItem>? ScanDirectory(string folder, System.Threading.CancellationToken token)
    {
        var list = new List<ImageItem>();
        if (token.IsCancellationRequested)
        {
            return null;
        }

        AppendFolderEntries(list, folder, token);
        if (token.IsCancellationRequested) return null;
        AppendFileEntries(list, folder, token);

        return list;
    }

    private static void AppendFolderEntries(List<ImageItem> list, string folder, System.Threading.CancellationToken token)
    {
        ExecuteIoSafe(
            () =>
        {
            var directories = new List<string>();
            foreach (var directory in Directory.EnumerateDirectories(folder, "*", TopLevelIgnoreInaccessibleOptions))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var name = Path.GetFileName(directory);
                if (name.StartsWith(".", StringComparison.Ordinal) || IsHiddenFile(directory))
                {
                    continue;
                }

                directories.Add(directory);
            }

            directories.Sort(StringComparer.OrdinalIgnoreCase);
            foreach (var dir in directories)
            {
                var modified = GetModifiedTime(dir);
                list.Add(new ImageItem(dir, null, isFolder: true, pageCount: 0, modified: modified, isImage: false));
            }
        },
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                $"ImageManager: Folder scan failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private static void AppendFileEntries(List<ImageItem> list, string folder, System.Threading.CancellationToken token)
    {
        ExecuteIoSafe(
            () =>
        {
            var pdfs = new List<string>();
            var images = new List<string>();
            foreach (var file in Directory.EnumerateFiles(folder, "*.*", TopLevelIgnoreInaccessibleOptions))
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }
                if (IsDotPrefixedFileName(file.AsSpan()))
                {
                    continue;
                }

                var kind = GetMediaFileKind(file.AsSpan());
                if (kind == MediaFileKind.None)
                {
                    continue;
                }
                if (IsHiddenFile(file))
                {
                    continue;
                }
                if (kind == MediaFileKind.Pdf)
                {
                    pdfs.Add(file);
                    continue;
                }
                if (kind == MediaFileKind.Image)
                {
                    images.Add(file);
                }
            }

            pdfs.Sort(StringComparer.OrdinalIgnoreCase);
            images.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (var file in pdfs)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var modified = GetModifiedTime(file);
                // Defer heavy PDF metadata probing to thumbnail worker to keep initial folder scan responsive.
                list.Add(new ImageItem(file, null, isFolder: false, pageCount: 0, modified: modified, isImage: false));
            }

            foreach (var file in images)
            {
                if (token.IsCancellationRequested)
                {
                    return;
                }

                var modified = GetModifiedTime(file);
                list.Add(new ImageItem(file, null, isFolder: false, pageCount: 0, modified: modified, isImage: true));
            }
        },
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                $"ImageManager: File scan failed: {ex.GetType().Name} - {ex.Message}"));
    }

    private static bool IsDotPrefixedFileName(ReadOnlySpan<char> path)
    {
        if (path.IsEmpty)
        {
            return false;
        }

        var separatorIndex = path.LastIndexOfAny('\\', '/');
        var fileName = separatorIndex >= 0 ? path[(separatorIndex + 1)..] : path;
        return !fileName.IsEmpty && fileName[0] == '.';
    }
}
