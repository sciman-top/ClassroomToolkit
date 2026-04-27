using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Media.Imaging;
using PdfiumViewer;

namespace ClassroomToolkit.App.Photos;

internal sealed class PdfDocumentHost : IDisposable
{
    private PdfDocument? _document;

    private PdfDocumentHost(PdfDocument document)
    {
        _document = document;
    }

    public int PageCount => _document?.PageCount ?? 0;

    public static PdfDocumentHost Open(string path)
    {
        var doc = PdfDocument.Load(path);
        return new PdfDocumentHost(doc);
    }

    public bool TryGetPageSize(int pageIndex, out SizeF size)
    {
        size = default;
        if (_document == null || _document.PageCount <= 0)
        {
            return false;
        }
        var page = Math.Clamp(pageIndex, 1, _document.PageCount) - 1;
        size = _document.PageSizes[page];
        return true;
    }

    public BitmapSource? RenderPage(int pageIndex, double dpi)
    {
        using var bitmap = RenderPageBitmap(pageIndex, dpi);
        if (bitmap == null)
        {
            return null;
        }
        var hBitmap = bitmap.GetHbitmap();
        try
        {
            var source = System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    public Bitmap? RenderPageBitmap(int pageIndex, double dpi)
    {
        if (_document == null)
        {
            return null;
        }
        var page = Math.Clamp(pageIndex, 1, _document.PageCount) - 1;
        var size = _document.PageSizes[page];
        var width = Math.Max(1, (int)Math.Round(size.Width / 72.0 * dpi));
        var height = Math.Max(1, (int)Math.Round(size.Height / 72.0 * dpi));
        using var image = _document.Render(page, width, height, (int)dpi, (int)dpi, PdfRenderFlags.Annotations);
        return new Bitmap(image);
    }

    public void Dispose()
    {
        _document?.Dispose();
        _document = null;
    }

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);
}
