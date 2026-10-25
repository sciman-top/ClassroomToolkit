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

        // .webp 不入列：目标平台 WIC 无内置 WebP 解码器（Store 可选扩展在锁定机房
        // 普遍缺失），列入会产生"看得到、打不开"的死条目。
        if (extension.Equals(".png".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpg".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jpeg".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".bmp".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".gif".AsSpan(), StringComparison.OrdinalIgnoreCase))
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
                ImageManagerPolicies.FormatFileAttributeReadFailureMessage(
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
                ImageManagerPolicies.FormatThumbnailLoadFailureMessage(
                    path,
                    sourceType: "image",
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static ImageSource? LoadThumbnail(byte[] content, int decodePixelWidth)
    {
        return ExecuteIoSafe<ImageSource?>(
            () =>
        {
            var bitmap = new BitmapImage();
            using var stream = new MemoryStream(content, writable: false);
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        },
            fallback: null,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerPolicies.FormatThumbnailLoadFailureMessage(
                    "<memory>",
                    sourceType: "image",
                    ex.GetType().Name,
                    ex.Message)));
    }

    internal static (ImageSource? Thumbnail, int PageCount) LoadPdfPreview(
        string path,
        int decodePixelWidth)
    {
        return ExecuteIoSafe(
            () =>
        {
            using var doc = PdfDocumentHost.Open(path);
                return (doc.RenderPage(1, ResolvePdfThumbnailDpi(doc, decodePixelWidth)), doc.PageCount);
        },
            fallback: (null, 0),
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerPolicies.FormatThumbnailLoadFailureMessage(
                    path,
                    sourceType: "pdf",
                    ex.GetType().Name,
                    ex.Message)));
    }

    private static double ResolvePdfThumbnailDpi(PdfDocumentHost document, int decodePixelWidth)
    {
        const double defaultDpi = 96;
        if (decodePixelWidth <= 0
            || !document.TryGetPageSize(1, out var pageSize)
            || pageSize.Width <= 0)
        {
            return defaultDpi;
        }

        // TryGetPageSize returns PDF points, while PdfDocumentHost scales the
        // page's 96-DIP width by dpi/96. Convert the target width accordingly.
        return Math.Min(defaultDpi, decodePixelWidth * 72.0 / pageSize.Width);
    }

    private static void ExecuteIoSafe(Action action, Action<Exception> onFailure)
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentNullException.ThrowIfNull(onFailure);

        try
        {
            action();
        }
        catch (Exception ex) when (AppGlobalExceptionHandling.IsNonFatal(ex))
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
        catch (Exception ex) when (AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            onFailure(ex);
            return fallback;
        }
    }

    private static List<ImageItem>? ScanDirectory(string folder, System.Threading.CancellationToken token)
    {
        var list = new List<ImageItem>();
        if (token.IsCancellationRequested)
        {
            return null;
        }

        var directories = new List<(string Path, DateTime Modified)>();
        var pdfs = new List<(string Path, DateTime Modified)>();
        var images = new List<(string Path, DateTime Modified)>();
        ExecuteIoSafe(
            () =>
            {
                var directory = new DirectoryInfo(folder);
                foreach (var entry in directory.EnumerateFileSystemInfos(
                             "*",
                             TopLevelIgnoreInaccessibleOptions))
                {
                    if (token.IsCancellationRequested)
                    {
                        return;
                    }

                    var name = entry.Name;
                    if (name.StartsWith('.') || IsHiddenEntry(entry))
                    {
                        continue;
                    }

                    if (entry is DirectoryInfo)
                    {
                        directories.Add((entry.FullName, entry.LastWriteTime));
                        continue;
                    }

                    var kind = GetMediaFileKind(entry.FullName.AsSpan());
                    if (kind == MediaFileKind.None)
                    {
                        continue;
                    }

                    var target = kind == MediaFileKind.Pdf ? pdfs : images;
                    target.Add((entry.FullName, entry.LastWriteTime));
                }
            },
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                $"ImageManager: Directory scan failed: {ex.GetType().Name} - {ex.Message}"));

        if (token.IsCancellationRequested)
        {
            return null;
        }

        directories.Sort(static (left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
        pdfs.Sort(static (left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));
        images.Sort(static (left, right) => string.Compare(left.Path, right.Path, StringComparison.OrdinalIgnoreCase));

        foreach (var entry in directories)
        {
            if (token.IsCancellationRequested)
            {
                return null;
            }

            list.Add(new ImageItem(entry.Path, null, isFolder: true, pageCount: 0, modified: entry.Modified, isImage: false));
        }

        foreach (var entry in pdfs)
        {
            if (token.IsCancellationRequested)
            {
                return null;
            }

            list.Add(new ImageItem(entry.Path, null, isFolder: false, pageCount: 0, modified: entry.Modified, isImage: false));
        }

        foreach (var entry in images)
        {
            if (token.IsCancellationRequested)
            {
                return null;
            }

            list.Add(new ImageItem(entry.Path, null, isFolder: false, pageCount: 0, modified: entry.Modified, isImage: true));
        }

        return list;
    }

    private static bool IsHiddenEntry(FileSystemInfo entry)
    {
        return ExecuteIoSafe(
            () => (entry.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden,
            fallback: false,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                ImageManagerPolicies.FormatFileAttributeReadFailureMessage(
                    entry.FullName,
                    ex.GetType().Name,
                    ex.Message)));
    }

}
