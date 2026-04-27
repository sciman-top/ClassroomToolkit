using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Ink;

public sealed partial class InkExportService
{
    private static BitmapSource CompositeImage(BitmapSource background, List<InkStrokeData> strokes)
    {
        int pixelWidth = background.PixelWidth;
        int pixelHeight = background.PixelHeight;
        double dpiX = background.DpiX;
        double dpiY = background.DpiY;
        if (dpiX <= 0)
        {
            dpiX = 96.0;
        }
        if (dpiY <= 0)
        {
            dpiY = 96.0;
        }
        var widthDip = pixelWidth * 96.0 / dpiX;
        var heightDip = pixelHeight * 96.0 / dpiY;

        var page = new InkPageData { Strokes = strokes };
        var renderer = new InkStrokeRenderer();
        var inkLayer = renderer.RenderPage(page, pixelWidth, pixelHeight, dpiX, dpiY);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(background, new Rect(0, 0, widthDip, heightDip));
            dc.DrawImage(inkLayer, new Rect(0, 0, widthDip, heightDip));
        }

        var result = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    private static BitmapSource? LoadBackground(string sourcePath, int pageIndex, int dpi)
    {
        if (IsPdf(sourcePath))
        {
            return LoadPdfPage(sourcePath, pageIndex, dpi);
        }
        return LoadImage(sourcePath);
    }

    private static BitmapSource? LoadPdfPage(string sourcePath, int pageIndex, int dpi)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(sourcePath);
            return doc.RenderPage(pageIndex, dpi);
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return null;
        }
    }

    private static BitmapSource? LoadImage(string sourcePath)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(sourcePath, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return null;
        }
    }

    private static int GetPdfPageCount(string sourcePath)
    {
        try
        {
            using var doc = PdfDocumentHost.Open(sourcePath);
            return doc.PageCount;
        }
        catch (Exception ex) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return 0;
        }
    }

    private static void SaveImage(BitmapSource bitmap, string outputPath, InkExportOptions options)
    {
        var directory = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        BitmapEncoder encoder;
        var ext = Path.GetExtension(outputPath);
        if (string.Equals(ext, ".jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".jpeg", StringComparison.OrdinalIgnoreCase))
        {
            encoder = new JpegBitmapEncoder
            {
                QualityLevel = Math.Clamp(options.JpegQuality, 1, 100)
            };
        }
        else
        {
            encoder = new PngBitmapEncoder();
        }

        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        encoder.Save(stream);
    }
}
