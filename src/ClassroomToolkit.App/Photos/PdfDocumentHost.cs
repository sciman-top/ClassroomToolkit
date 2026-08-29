using System;
using System.Drawing;
using System.IO;
using System.Windows.Media.Imaging;
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace ClassroomToolkit.App.Photos;

internal interface IPdfDocumentHost : IDisposable
{
    int PageCount { get; }

    bool TryGetPageSize(int pageIndex, out SizeF size);

    BitmapSource? RenderPage(int pageIndex, double dpi);
}

internal sealed class PdfDocumentHost : IPdfDocumentHost
{
    internal const uint MaxRenderDimension = 16_384;
    internal const ulong MaxRenderPixels = 32UL * 1024 * 1024;

    private PdfDocument? _document;

    private PdfDocumentHost(PdfDocument document)
    {
        _document = document;
    }

    public int PageCount => _document == null ? 0 : checked((int)_document.PageCount);

    public static PdfDocumentHost Open(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var file = StorageFile
            .GetFileFromPathAsync(Path.GetFullPath(path))
            .AsTask()
            .GetAwaiter()
            .GetResult();
        var document = PdfDocument
            .LoadFromFileAsync(file)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        return new PdfDocumentHost(document);
    }

    public bool TryGetPageSize(int pageIndex, out SizeF size)
    {
        size = default;
        var document = _document;
        if (document == null || document.PageCount == 0)
        {
            return false;
        }

        var pageIndexZeroBased = Math.Clamp(pageIndex, 1, checked((int)document.PageCount)) - 1;
        using var page = document.GetPage(checked((uint)pageIndexZeroBased));
        size = new SizeF(
            checked((float)(page.Size.Width * 72.0 / 96.0)),
            checked((float)(page.Size.Height * 72.0 / 96.0)));
        return true;
    }

    public BitmapSource? RenderPage(int pageIndex, double dpi)
    {
        var document = _document;
        if (document == null || document.PageCount == 0 || !double.IsFinite(dpi) || dpi <= 0)
        {
            return null;
        }

        var pageIndexZeroBased = Math.Clamp(pageIndex, 1, checked((int)document.PageCount)) - 1;
        using var page = document.GetPage(checked((uint)pageIndexZeroBased));
        if (!TryCalculateRenderDimensions(page, dpi, out var width, out var height))
        {
            return null;
        }

        var options = new PdfPageRenderOptions
        {
            DestinationWidth = width,
            DestinationHeight = height,
            BackgroundColor = Windows.UI.Color.FromArgb(255, 255, 255, 255)
        };

        using var randomAccessStream = new InMemoryRandomAccessStream();
        page.RenderToStreamAsync(randomAccessStream, options)
            .AsTask()
            .GetAwaiter()
            .GetResult();
        randomAccessStream.Seek(0);
        using var stream = randomAccessStream.AsStreamForRead();
        var source = BitmapFrame.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        source.Freeze();
        return source;
    }

    public void Dispose()
    {
        // Windows.Data.Pdf.PdfDocument 的 SDK 投影（10.0.19041.57）未暴露 Close/Dispose
        //（同文件 PdfPage/IRandomAccessStream 均可实现 using）。置空引用后由投影对象的
        // 终结器释放原生引用；翻阅大量 PDF 时回收会略有延迟，属可接受权衡。
        _document = null;
    }

    private static bool TryCalculateRenderDimensions(
        PdfPage page,
        double dpi,
        out uint width,
        out uint height)
    {
        var widthPixels = Math.Max(1, Math.Round(page.Size.Width * dpi / 96.0));
        var heightPixels = Math.Max(1, Math.Round(page.Size.Height * dpi / 96.0));
        if (!double.IsFinite(widthPixels)
            || !double.IsFinite(heightPixels)
            || widthPixels > MaxRenderDimension
            || heightPixels > MaxRenderDimension
            || widthPixels * heightPixels > MaxRenderPixels)
        {
            width = 0;
            height = 0;
            return false;
        }

        width = checked((uint)widthPixels);
        height = checked((uint)heightPixels);
        return true;
    }
}
