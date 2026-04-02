using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
        _tempDir = TestPathHelper.CreateDirectory("ctk_export_test");
        Directory.CreateDirectory(_tempDir);
        _service = new InkExportService(new InkPersistenceService());
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenPersistenceServiceIsNull()
    {
        Action act = () => _ = new InkExportService(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportAllInDirectory_ShouldThrow_WhenOptionsIsNull()
    {
        Action act = () => _service.ExportAllInDirectory(_tempDir, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void ExportSinglePage_ShouldThrow_WhenOptionsIsNull()
    {
        Action act = () => _service.ExportSinglePage("a.png", 1, new List<InkStrokeData>(), null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void GetExistingOutputPaths_ShouldThrow_WhenOptionsIsNull()
    {
        Action act = () => _service.GetExistingOutputPaths("a.png", inkDoc: null, options: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void NormalizePathOrOriginal_ShouldReturnInput_WhenPathIsInvalid()
    {
        var method = typeof(InkExportService).GetMethod(
            "NormalizePathOrOriginal",
            BindingFlags.Static | BindingFlags.NonPublic);
        method.Should().NotBeNull();

        var act = () => (string)method!.Invoke(null, ["\0invalid-path"])!;

        var normalized = act.Should().NotThrow().Subject;
        normalized.Should().Be("\0invalid-path");
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

    [Fact]
    public void BuildExportFingerprint_ShouldChange_WhenCalligraphyCompositeDetailsChange()
    {
        var sourcePath = Path.Combine(_tempDir, "fingerprint.png");
        File.WriteAllText(sourcePath, "dummy");
        var options = new InkExportOptions();
        var baseStroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = ClassroomToolkit.App.Paint.PaintBrushStyle.Calligraphy,
            ColorHex = "#112233",
            Opacity = 200,
            BrushSize = 8.5,
            GeometryPath = "M 0,0 L 10,10",
            ReferenceWidth = 100,
            ReferenceHeight = 80,
            CalligraphyRenderMode = ClassroomToolkit.App.Paint.CalligraphyRenderMode.Ink,
            CalligraphyInkBloomEnabled = true,
            CalligraphySealEnabled = true,
            CalligraphyOverlayOpacityThreshold = 210,
            MaskSeed = 42,
            InkFlow = 0.67,
            StrokeDirectionX = 0.4,
            StrokeDirectionY = -0.2
        };
        baseStroke.Ribbons.Add(new InkRibbonData
        {
            RibbonT = 0.5,
            Opacity = 0.3,
            GeometryPath = "M 1,1 L 2,2"
        });
        baseStroke.Blooms.Add(new InkBloomData
        {
            Opacity = 0.2,
            GeometryPath = "M 2,2 L 3,3"
        });

        var fingerprintA = BuildExportFingerprintViaReflection(sourcePath, 1, new List<InkStrokeData> { baseStroke }, options);

        var changedRibbon = CloneStrokeForFingerprint(baseStroke);
        changedRibbon.Ribbons[0].GeometryPath = "M 1,1 L 4,4";
        var fingerprintB = BuildExportFingerprintViaReflection(sourcePath, 1, new List<InkStrokeData> { changedRibbon }, options);

        var changedMaskSeed = CloneStrokeForFingerprint(baseStroke);
        changedMaskSeed.MaskSeed = 43;
        var fingerprintC = BuildExportFingerprintViaReflection(sourcePath, 1, new List<InkStrokeData> { changedMaskSeed }, options);

        fingerprintB.Should().NotBe(fingerprintA);
        fingerprintC.Should().NotBe(fingerprintA);
    }

    [Fact]
    public void BuildExportFingerprint_ShouldStayStable_ForEquivalentStrokePayload()
    {
        var sourcePath = Path.Combine(_tempDir, "fingerprint-stable.png");
        File.WriteAllText(sourcePath, "dummy");
        var options = new InkExportOptions();
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Shape,
            BrushStyle = ClassroomToolkit.App.Paint.PaintBrushStyle.StandardRibbon,
            ColorHex = "#ABCDEF",
            Opacity = 255,
            BrushSize = 4,
            GeometryPath = "M 0,0 L 1,1",
            ReferenceWidth = 120,
            ReferenceHeight = 90
        };

        var fingerprint1 = BuildExportFingerprintViaReflection(sourcePath, 1, new List<InkStrokeData> { stroke }, options);
        var fingerprint2 = BuildExportFingerprintViaReflection(sourcePath, 1, new List<InkStrokeData> { CloneStrokeForFingerprint(stroke) }, options);

        fingerprint1.Should().Be(fingerprint2);
    }

    [Fact]
    public void ExportAllPagesForFile_ShouldReExport_WhenCalligraphyOverlayPayloadChanges()
    {
        var sourcePath = Path.Combine(_tempDir, "calligraphy-export.png");
        SaveSolidPng(sourcePath, 200, 200, Colors.White);

        var baseStroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = ClassroomToolkit.App.Paint.PaintBrushStyle.Calligraphy,
            ColorHex = "#222222",
            Opacity = 255,
            BrushSize = 8,
            GeometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(20, 20, 60, 30))),
            ReferenceWidth = 200,
            ReferenceHeight = 200,
            CalligraphyRenderMode = ClassroomToolkit.App.Paint.CalligraphyRenderMode.Ink,
            CalligraphyInkBloomEnabled = true,
            CalligraphySealEnabled = true
        };
        baseStroke.Ribbons.Add(new InkRibbonData
        {
            GeometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(24, 24, 50, 20))),
            Opacity = 0.22,
            RibbonT = 0.5
        });

        var docV1 = new InkDocumentData
        {
            SourcePath = sourcePath,
            Pages = new List<InkPageData>
            {
                new()
                {
                    PageIndex = 1,
                    Strokes = new List<InkStrokeData> { CloneStrokeForFingerprint(baseStroke) }
                }
            }
        };

        var first = _service.ExportAllPagesForFile(sourcePath, docV1, new InkExportOptions());
        first.Should().ContainSingle();
        var outputPath = first[0];
        var firstWrite = File.GetLastWriteTimeUtc(outputPath);

        Thread.Sleep(40);

        var changedStroke = CloneStrokeForFingerprint(baseStroke);
        changedStroke.Ribbons[0].GeometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(24, 24, 55, 20)));
        var docV2 = new InkDocumentData
        {
            SourcePath = sourcePath,
            Pages = new List<InkPageData>
            {
                new()
                {
                    PageIndex = 1,
                    Strokes = new List<InkStrokeData> { changedStroke }
                }
            }
        };

        var second = _service.ExportAllPagesForFile(sourcePath, docV2, new InkExportOptions());
        second.Should().ContainSingle();
        second[0].Should().Be(outputPath);
        var secondWrite = File.GetLastWriteTimeUtc(outputPath);
        secondWrite.Should().BeAfter(firstWrite);
    }

    private static InkStrokeData CloneStrokeForFingerprint(InkStrokeData stroke)
    {
        var clone = new InkStrokeData
        {
            Type = stroke.Type,
            BrushStyle = stroke.BrushStyle,
            GeometryPath = stroke.GeometryPath,
            ColorHex = stroke.ColorHex,
            Opacity = stroke.Opacity,
            BrushSize = stroke.BrushSize,
            MaskSeed = stroke.MaskSeed,
            InkFlow = stroke.InkFlow,
            StrokeDirectionX = stroke.StrokeDirectionX,
            StrokeDirectionY = stroke.StrokeDirectionY,
            CalligraphyRenderMode = stroke.CalligraphyRenderMode,
            ReferenceWidth = stroke.ReferenceWidth,
            ReferenceHeight = stroke.ReferenceHeight,
            CalligraphyInkBloomEnabled = stroke.CalligraphyInkBloomEnabled,
            CalligraphySealEnabled = stroke.CalligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = stroke.CalligraphyOverlayOpacityThreshold
        };
        foreach (var ribbon in stroke.Ribbons)
        {
            clone.Ribbons.Add(new InkRibbonData
            {
                GeometryPath = ribbon.GeometryPath,
                Opacity = ribbon.Opacity,
                RibbonT = ribbon.RibbonT
            });
        }
        foreach (var bloom in stroke.Blooms)
        {
            clone.Blooms.Add(new InkBloomData
            {
                GeometryPath = bloom.GeometryPath,
                Opacity = bloom.Opacity
            });
        }
        return clone;
    }

    private static string BuildExportFingerprintViaReflection(
        string sourcePath,
        int pageIndex,
        IReadOnlyList<InkStrokeData> strokes,
        InkExportOptions options)
    {
        var method = typeof(InkExportService).GetMethod(
            "BuildExportFingerprint",
            BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var value = method!.Invoke(null, new object?[] { sourcePath, pageIndex, strokes, options });
        value.Should().BeOfType<string>();
        return (string)value!;
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
