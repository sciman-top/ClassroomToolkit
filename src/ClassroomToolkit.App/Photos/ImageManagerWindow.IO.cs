using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.Interop;

namespace ClassroomToolkit.App.Photos;

public partial class ImageManagerWindow
{
    private static bool IsPdfFile(string path)
    {
        var ext = Path.GetExtension(path);
        return ext != null && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsHiddenFile(string path)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
        }
        catch
        {
            return false;
        }
    }

    private static ImageSource? LoadThumbnail(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 240;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private static ImageSource? LoadPdfThumbnail(string path)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(path);
            return doc.RenderPage(1, 96);
        }
        catch
        {
            return null;
        }
    }

    private static int TryGetPdfPageCount(string path)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(path);
            return doc.PageCount;
        }
        catch
        {
            return 0;
        }
    }

    private static DateTime GetModifiedTime(string path)
    {
        try
        {
            return File.GetLastWriteTime(path);
        }
        catch
        {
            return DateTime.MinValue;
        }
    }

    private List<ImageItem>? ScanDirectory(string folder, System.Threading.CancellationToken token)
    {
        var list = new List<ImageItem>();
        try
        {
            // 1. Folders
            var dirs = Directory.EnumerateDirectories(folder, "*", SearchOption.TopDirectoryOnly)
                .Where(d => !IsHiddenFile(d) && !Path.GetFileName(d).StartsWith("."))
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase);

            foreach (var dir in dirs)
            {
                if (token.IsCancellationRequested) return null;
                var modified = GetModifiedTime(dir);
                list.Add(new ImageItem(dir, null, isFolder: true, pageCount: 0, modified: modified, isImage: false));
            }

            // 2. PDFs
            var pdfs = Directory.EnumerateFiles(folder, "*.pdf", SearchOption.TopDirectoryOnly)
                .Where(f => !IsHiddenFile(f))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var file in pdfs)
            {
                if (token.IsCancellationRequested) return null;
                var pageCount = TryGetPdfPageCount(file);
                var modified = GetModifiedTime(file);
                list.Add(new ImageItem(file, null, isFolder: false, pageCount: pageCount, modified: modified, isImage: false));
            }

            // 3. Images
            var imageExtensions = new[] { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp" };
            var imgs = Directory.EnumerateFiles(folder, "*.*", SearchOption.TopDirectoryOnly)
                .Where(f => !IsHiddenFile(f) && imageExtensions.Contains(Path.GetExtension(f)?.ToLowerInvariant()))
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);

            foreach (var file in imgs)
            {
                if (token.IsCancellationRequested) return null;
                var modified = GetModifiedTime(file);
                list.Add(new ImageItem(file, null, isFolder: false, pageCount: 0, modified: modified, isImage: true));
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ImageManager: IO Error: {ex.Message}");
        }
        return list;
    }
}
