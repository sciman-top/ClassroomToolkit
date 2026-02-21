using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using ClassroomToolkit.App.Ink;
using FluentAssertions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkExportServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InkExportService _service;

    public InkExportServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ctk_export_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _service = new InkExportService(new InkPersistenceService());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void ListFilesWithCompositeExports_ShouldResolvePdfAndImageSources()
    {
        var pdfPath = Path.Combine(_tempDir, "lesson.pdf");
        var pngPath = Path.Combine(_tempDir, "board.png");
        var jpgPath = Path.Combine(_tempDir, "photo.jpg");
        File.WriteAllText(pdfPath, "dummy");
        File.WriteAllText(pngPath, "dummy");
        File.WriteAllText(jpgPath, "dummy");

        var exportDir = Path.Combine(_tempDir, "笔迹合成图片");
        Directory.CreateDirectory(exportDir);
        File.WriteAllText(Path.Combine(exportDir, "lesson_P001+笔迹.png"), "x");
        File.WriteAllText(Path.Combine(exportDir, "lesson_P002+笔迹.jpg"), "x");
        File.WriteAllText(Path.Combine(exportDir, "board+笔迹.png"), "x");
        File.WriteAllText(Path.Combine(exportDir, "photo+笔迹.jpg"), "x");
        File.WriteAllText(Path.Combine(exportDir, "ghost+笔迹.png"), "x");
        File.WriteAllText(Path.Combine(exportDir, "invalid_name.png"), "x");
        File.WriteAllText(Path.Combine(exportDir, "lesson_P0A1+笔迹.png"), "x");

        var result = _service.ListFilesWithCompositeExports(_tempDir);

        result.Should().HaveCount(3);
        result.Should().Contain(pdfPath);
        result.Should().Contain(pngPath);
        result.Should().Contain(jpgPath);
    }

    [Fact]
    public void ListFilesWithCompositeExports_ShouldReturnEmpty_WhenExportFolderMissing()
    {
        var result = _service.ListFilesWithCompositeExports(_tempDir);
        result.Should().BeEmpty();
    }

    [Fact]
    public void ExportAllPagesForFile_Image_ShouldMapStrokeFromReferenceSizeToSourceSize()
    {
        var sourcePath = Path.Combine(_tempDir, "source.png");
        SaveSolidPng(sourcePath, 200, 200, Colors.White);

        var strokeGeometry = new RectangleGeometry(new Rect(10, 10, 20, 20));
        var inkDoc = new InkDocumentData
        {
            SourcePath = sourcePath,
            Pages = new List<InkPageData>
            {
                new()
                {
                    PageIndex = 1,
                    Strokes = new List<InkStrokeData>
                    {
                        new()
                        {
                            Type = InkStrokeType.Shape,
                            BrushStyle = ClassroomToolkit.App.Paint.PaintBrushStyle.StandardRibbon,
                            GeometryPath = InkGeometrySerializer.Serialize(strokeGeometry),
                            ColorHex = "#FF0000",
                            Opacity = 255,
                            BrushSize = 1,
                            ReferenceWidth = 100,
                            ReferenceHeight = 100
                        }
                    }
                }
            }
        };

        var outputs = _service.ExportAllPagesForFile(sourcePath, inkDoc, new InkExportOptions());
        outputs.Should().ContainSingle();

        var exported = LoadBitmap(outputs[0]);
        ReadPixel(exported, 30, 30).Should().BeEquivalentTo(new[] { (byte)0, (byte)0, (byte)255, (byte)255 });
        ReadPixel(exported, 12, 12).Should().BeEquivalentTo(new[] { (byte)255, (byte)255, (byte)255, (byte)255 });
    }

    [Fact]
    public void ExportAllPagesForFile_ImageHighDpi_ShouldKeepBackgroundContentWithoutCropping()
    {
        var sourcePath = Path.Combine(_tempDir, "highdpi.png");
        SaveSplitPng(sourcePath, 300, 100, 300, Colors.Red, Colors.Blue);

        var strokeGeometry = new RectangleGeometry(new Rect(40, 10, 10, 10));
        var inkDoc = new InkDocumentData
        {
            SourcePath = sourcePath,
            Pages = new List<InkPageData>
            {
                new()
                {
                    PageIndex = 1,
                    Strokes = new List<InkStrokeData>
                    {
                        new()
                        {
                            Type = InkStrokeType.Shape,
                            BrushStyle = ClassroomToolkit.App.Paint.PaintBrushStyle.StandardRibbon,
                            GeometryPath = InkGeometrySerializer.Serialize(strokeGeometry),
                            ColorHex = "#00FF00",
                            Opacity = 255,
                            BrushSize = 1,
                            ReferenceWidth = 96,
                            ReferenceHeight = 32
                        }
                    }
                }
            }
        };

        var outputs = _service.ExportAllPagesForFile(sourcePath, inkDoc, new InkExportOptions());
        outputs.Should().ContainSingle();

        var exported = LoadBitmap(outputs[0]);
        ReadPixel(exported, 10, 50).Should().BeEquivalentTo(new[] { (byte)0, (byte)0, (byte)255, (byte)255 });
        ReadPixel(exported, 260, 50).Should().BeEquivalentTo(new[] { (byte)255, (byte)0, (byte)0, (byte)255 });
    }

    [Fact]
    public void ExportAllPagesForFile_Image_ShouldReuseExistingOutput_WhenFingerprintUnchanged()
    {
        var sourcePath = Path.Combine(_tempDir, "stable.png");
        SaveSolidPng(sourcePath, 120, 120, Colors.White);

        var inkDoc = new InkDocumentData
        {
            SourcePath = sourcePath,
            Pages = new List<InkPageData>
            {
                new()
                {
                    PageIndex = 1,
                    Strokes = new List<InkStrokeData>
                    {
                        new()
                        {
                            Type = InkStrokeType.Shape,
                            BrushStyle = ClassroomToolkit.App.Paint.PaintBrushStyle.StandardRibbon,
                            GeometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(10, 10, 20, 20))),
                            ColorHex = "#FF0000",
                            Opacity = 255,
                            BrushSize = 1,
                            ReferenceWidth = 120,
                            ReferenceHeight = 120
                        }
                    }
                }
            }
        };

        var first = _service.ExportAllPagesForFile(sourcePath, inkDoc, new InkExportOptions());
        first.Should().ContainSingle();
        var output = first[0];
        File.Exists(output).Should().BeTrue();
        var firstWrite = File.GetLastWriteTimeUtc(output);

        Thread.Sleep(40);

        var second = _service.ExportAllPagesForFile(sourcePath, inkDoc, new InkExportOptions());
        second.Should().ContainSingle();
        second[0].Should().Be(output);
        var secondWrite = File.GetLastWriteTimeUtc(output);
        secondWrite.Should().Be(firstWrite);
    }

    [Fact]
    public void ExportAllPagesForFile_PdfWithoutInk_ShouldDeleteStaleCompositeOutputs()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson.pdf");
        File.WriteAllText(sourcePath, "not a real pdf");

        var exportDir = Path.Combine(_tempDir, "笔迹合成图片");
        Directory.CreateDirectory(exportDir);

        var stalePage1 = Path.Combine(exportDir, "lesson_P001+笔迹.png");
        var stalePage2 = Path.Combine(exportDir, "lesson_P002+笔迹.png");
        var otherFile = Path.Combine(exportDir, "other_P001+笔迹.png");
        File.WriteAllText(stalePage1, "stale-1");
        File.WriteAllText(stalePage2, "stale-2");
        File.WriteAllText(otherFile, "other");

        var manifestPath = Path.Combine(exportDir, ".ink-export.manifest.json");
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lesson_P001+笔迹.png"] = "hash1",
            ["lesson_P002+笔迹.png"] = "hash2",
            ["other_P001+笔迹.png"] = "hash3"
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        var inkDoc = new InkDocumentData
        {
            SourcePath = sourcePath,
            Pages = new List<InkPageData>()
        };

        var result = _service.ExportAllPagesForFileDetailed(sourcePath, inkDoc, new InkExportOptions());

        result.ExportedCount.Should().Be(0);
        File.Exists(stalePage1).Should().BeFalse();
        File.Exists(stalePage2).Should().BeFalse();
        File.Exists(otherFile).Should().BeTrue();

        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath));
        persisted.Should().NotBeNull();
        persisted!.Keys.Should().NotContain("lesson_P001+笔迹.png");
        persisted.Keys.Should().NotContain("lesson_P002+笔迹.png");
        persisted.Keys.Should().Contain("other_P001+笔迹.png");
    }

    [Fact]
    public void ExportAllInDirectory_NoInkSidecar_ShouldCleanupStaleCompositeOutputs()
    {
        var sourceImage = Path.Combine(_tempDir, "photo.png");
        File.WriteAllText(sourceImage, "dummy");

        var otherImage = Path.Combine(_tempDir, "other.png");
        File.WriteAllText(otherImage, "dummy");

        var exportDir = Path.Combine(_tempDir, "笔迹合成图片");
        Directory.CreateDirectory(exportDir);

        var stale = Path.Combine(exportDir, "photo+笔迹.png");
        var keep = Path.Combine(exportDir, "other+笔迹.png");
        File.WriteAllText(stale, "stale");
        File.WriteAllText(keep, "keep");

        var manifestPath = Path.Combine(exportDir, ".ink-export.manifest.json");
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["photo+笔迹.png"] = "hash_photo",
            ["other+笔迹.png"] = "hash_other"
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        var outputs = _service.ExportAllInDirectory(_tempDir, new InkExportOptions());

        outputs.Should().BeEmpty();
        File.Exists(stale).Should().BeFalse();
        File.Exists(keep).Should().BeFalse();

        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath));
        persisted.Should().NotBeNull();
        persisted!.Keys.Should().NotContain("photo+笔迹.png");
        persisted.Keys.Should().NotContain("other+笔迹.png");
    }

    [Fact]
    public void RemoveCompositeOutputsForPage_ShouldDeleteOnlyTargetPageForPdf()
    {
        var sourcePath = Path.Combine(_tempDir, "lesson.pdf");
        File.WriteAllText(sourcePath, "dummy");

        var exportDir = Path.Combine(_tempDir, "笔迹合成图片");
        Directory.CreateDirectory(exportDir);
        var page1 = Path.Combine(exportDir, "lesson_P001+笔迹.png");
        var page2 = Path.Combine(exportDir, "lesson_P002+笔迹.png");
        File.WriteAllText(page1, "p1");
        File.WriteAllText(page2, "p2");

        var manifestPath = Path.Combine(exportDir, ".ink-export.manifest.json");
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["lesson_P001+笔迹.png"] = "h1",
            ["lesson_P002+笔迹.png"] = "h2"
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        var deleted = _service.RemoveCompositeOutputsForPage(sourcePath, 1);

        deleted.Should().Be(1);
        File.Exists(page1).Should().BeFalse();
        File.Exists(page2).Should().BeTrue();

        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath));
        persisted.Should().NotBeNull();
        persisted!.Keys.Should().NotContain("lesson_P001+笔迹.png");
        persisted.Keys.Should().Contain("lesson_P002+笔迹.png");
    }

    [Fact]
    public void RemoveCompositeOutputsForPage_Image_ShouldDeleteCompositeAndManifestEntry()
    {
        var sourcePath = Path.Combine(_tempDir, "photo.png");
        File.WriteAllText(sourcePath, "dummy");

        var exportDir = Path.Combine(_tempDir, "笔迹合成图片");
        Directory.CreateDirectory(exportDir);
        var output = Path.Combine(exportDir, "photo+笔迹.png");
        File.WriteAllText(output, "img");

        var manifestPath = Path.Combine(exportDir, ".ink-export.manifest.json");
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["photo+笔迹.png"] = "h1"
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        var deleted = _service.RemoveCompositeOutputsForPage(sourcePath, 1);

        deleted.Should().Be(1);
        File.Exists(output).Should().BeFalse();
        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath));
        persisted.Should().NotBeNull();
        persisted!.Keys.Should().NotContain("photo+笔迹.png");
    }

    [Fact]
    public void CleanupOrphanCompositeOutputsInDirectory_ShouldDeleteOnlyOrphans()
    {
        var exists = Path.Combine(_tempDir, "exists.png");
        File.WriteAllText(exists, "dummy");
        var orphan = Path.Combine(_tempDir, "orphan.png");

        var exportDir = Path.Combine(_tempDir, "笔迹合成图片");
        Directory.CreateDirectory(exportDir);
        var existsComposite = Path.Combine(exportDir, "exists+笔迹.png");
        var orphanComposite = Path.Combine(exportDir, "orphan+笔迹.png");
        File.WriteAllText(existsComposite, "x");
        File.WriteAllText(orphanComposite, "x");

        var manifestPath = Path.Combine(exportDir, ".ink-export.manifest.json");
        var manifest = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["exists+笔迹.png"] = "h1",
            ["orphan+笔迹.png"] = "h2"
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));

        var deleted = _service.CleanupOrphanCompositeOutputsInDirectory(_tempDir);

        deleted.Should().Be(1);
        File.Exists(existsComposite).Should().BeTrue();
        File.Exists(orphanComposite).Should().BeFalse();
        var persisted = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(manifestPath));
        persisted.Should().NotBeNull();
        persisted!.Keys.Should().Contain("exists+笔迹.png");
        persisted.Keys.Should().NotContain("orphan+笔迹.png");
    }

    private static void SaveSolidPng(string path, int width, int height, Color color)
    {
        var bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = color.B;
            pixels[i + 1] = color.G;
            pixels[i + 2] = color.R;
            pixels[i + 3] = color.A;
        }

        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    private static void SaveSplitPng(string path, int width, int height, double dpi, Color leftColor, Color rightColor)
    {
        var bitmap = new WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, null);
        var pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                var color = x < width / 2 ? leftColor : rightColor;
                var i = (y * width + x) * 4;
                pixels[i] = color.B;
                pixels[i + 1] = color.G;
                pixels[i + 2] = color.R;
                pixels[i + 3] = color.A;
            }
        }

        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, width * 4, 0);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write);
        encoder.Save(fs);
    }

    private static BitmapSource LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path, UriKind.Absolute);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static byte[] ReadPixel(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel;
    }
}
