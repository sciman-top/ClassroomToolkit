using System;
using System.Collections.Generic;
using System.IO;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkPersistenceServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly InkPersistenceService _service;

    public InkPersistenceServiceTests()
    {
        _tempDir = TestPathHelper.CreateDirectory("ctk_ink_test");
        Directory.CreateDirectory(_tempDir);
        _service = new InkPersistenceService();
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempDir, recursive: true); } catch { }
    }

    private string CreateTempFile(string name = "test.png")
    {
        var path = Path.Combine(_tempDir, name);
        File.WriteAllText(path, "dummy");
        return path;
    }

    [Fact]
    public void SaveAndLoad_ShouldRoundTrip()
    {
        var filePath = CreateTempFile();
        var strokes = new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 3.0, GeometryPath = "M 0 0 L 10 10" }
        };

        _service.SaveInkForFile(filePath, 1, strokes);

        var doc = _service.LoadInkForFile(filePath);
        doc.Should().NotBeNull();
        doc!.Pages.Should().HaveCount(1);
        doc.Pages[0].PageIndex.Should().Be(1);
        doc.Pages[0].Strokes.Should().HaveCount(1);
        doc.Pages[0].Strokes[0].ColorHex.Should().Be("#FF0000");
    }

    [Fact]
    public void SaveAndLoad_ShouldPreserveCalligraphyRibbonLayers()
    {
        var filePath = CreateTempFile("calligraphy.png");
        var strokes = new List<InkStrokeData>
        {
            new()
            {
                Type = InkStrokeType.Brush,
                BrushStyle = PaintBrushStyle.Calligraphy,
                ColorHex = "#111111",
                BrushSize = 5.0,
                GeometryPath = "M 0 0 L 20 20",
                Ribbons = new List<InkRibbonData>
                {
                    new() { GeometryPath = "M 0 0 L 10 10", Opacity = 0.26, RibbonT = 0.0 },
                    new() { GeometryPath = "M 0 1 L 10 11", Opacity = 0.14, RibbonT = 1.0 }
                }
            }
        };

        _service.SaveInkForFile(filePath, 1, strokes);

        var doc = _service.LoadInkForFile(filePath);
        doc.Should().NotBeNull();
        doc!.Pages.Should().HaveCount(1);
        var loaded = doc.Pages[0].Strokes[0];
        loaded.Ribbons.Should().HaveCount(2);
        loaded.Ribbons[0].GeometryPath.Should().Be("M 0 0 L 10 10");
        loaded.Ribbons[0].Opacity.Should().BeApproximately(0.26, 0.0001);
        loaded.Ribbons[0].RibbonT.Should().BeApproximately(0.0, 0.0001);
        loaded.Ribbons[1].GeometryPath.Should().Be("M 0 1 L 10 11");
        loaded.Ribbons[1].Opacity.Should().BeApproximately(0.14, 0.0001);
        loaded.Ribbons[1].RibbonT.Should().BeApproximately(1.0, 0.0001);
    }

    [Fact]
    public void HasInk_ShouldReturnTrue_WhenInkExists()
    {
        var filePath = CreateTempFile();
        var strokes = new List<InkStrokeData>
        {
            new() { ColorHex = "#0000FF", BrushSize = 2.0, GeometryPath = "M 5 5 L 15 15" }
        };

        _service.HasInkForFile(filePath).Should().BeFalse();
        _service.SaveInkForFile(filePath, 1, strokes);
        _service.HasInkForFile(filePath).Should().BeTrue();
    }

    [Fact]
    public void DeleteInk_ShouldRemoveSidecar()
    {
        var filePath = CreateTempFile();
        var strokes = new List<InkStrokeData>
        {
            new() { ColorHex = "#00FF00", BrushSize = 1.0, GeometryPath = "M 0 0 L 5 5" }
        };

        _service.SaveInkForFile(filePath, 1, strokes);
        _service.HasInkForFile(filePath).Should().BeTrue();

        _service.DeleteInkForFile(filePath);
        _service.HasInkForFile(filePath).Should().BeFalse();
    }

    [Fact]
    public void SaveEmptyStrokes_ShouldDeleteSidecar()
    {
        var filePath = CreateTempFile();
        var strokes = new List<InkStrokeData>
        {
            new() { ColorHex = "#FFFFFF", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        };

        _service.SaveInkForFile(filePath, 1, strokes);
        _service.HasInkForFile(filePath).Should().BeTrue();

        // Save empty strokes — should clean up
        _service.SaveInkForFile(filePath, 1, new List<InkStrokeData>());
        _service.HasInkForFile(filePath).Should().BeFalse();
    }

    [Fact]
    public void SaveMultiplePages_ShouldMerge()
    {
        var filePath = CreateTempFile("lecture.pdf");
        var page1 = new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 2.0, GeometryPath = "M 0 0 L 10 10" }
        };
        var page2 = new List<InkStrokeData>
        {
            new() { ColorHex = "#0000FF", BrushSize = 3.0, GeometryPath = "M 5 5 L 15 15" }
        };

        _service.SaveInkForFile(filePath, 1, page1);
        _service.SaveInkForFile(filePath, 2, page2);

        var doc = _service.LoadInkForFile(filePath);
        doc.Should().NotBeNull();
        doc!.Pages.Should().HaveCount(2);
        doc.Pages.Should().ContainSingle(p => p.PageIndex == 1);
        doc.Pages.Should().ContainSingle(p => p.PageIndex == 2);
    }

    [Fact]
    public void LoadInkPageForFile_ShouldReturnOnlyRequestedPage()
    {
        var filePath = CreateTempFile("paged.pdf");
        _service.SaveInkForFile(filePath, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 2.0, GeometryPath = "M 0 0 L 1 1" }
        });
        _service.SaveInkForFile(filePath, 2, new List<InkStrokeData>
        {
            new() { ColorHex = "#00FF00", BrushSize = 3.0, GeometryPath = "M 2 2 L 3 3" }
        });

        var page2 = _service.LoadInkPageForFile(filePath, 2);
        page2.Should().NotBeNull();
        page2!.Should().HaveCount(1);
        page2![0].ColorHex.Should().Be("#00FF00");

        var missing = _service.LoadInkPageForFile(filePath, 3);
        missing.Should().BeNull();
    }

    [Fact]
    public void SaveEmptyPage_ShouldOnlyRemoveThatPage_AndKeepOtherPages()
    {
        var filePath = CreateTempFile("lesson.pdf");
        var page1 = new List<InkStrokeData>
        {
            new() { ColorHex = "#111111", BrushSize = 2.0, GeometryPath = "M 0 0 L 10 10" }
        };
        var page2 = new List<InkStrokeData>
        {
            new() { ColorHex = "#222222", BrushSize = 2.0, GeometryPath = "M 5 5 L 15 15" }
        };

        _service.SaveInkForFile(filePath, 1, page1);
        _service.SaveInkForFile(filePath, 2, page2);
        _service.SaveInkForFile(filePath, 1, new List<InkStrokeData>());

        var doc = _service.LoadInkForFile(filePath);
        doc.Should().NotBeNull();
        doc!.Pages.Should().HaveCount(1);
        doc.Pages[0].PageIndex.Should().Be(2);
        doc.Pages[0].Strokes.Should().HaveCount(1);
    }

    [Fact]
    public void ListFilesWithInk_ShouldReturnCorrectFiles()
    {
        var file1 = CreateTempFile("image1.png");
        var file2 = CreateTempFile("image2.jpg");
        CreateTempFile("image3.bmp"); // no ink

        _service.SaveInkForFile(file1, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        });
        _service.SaveInkForFile(file2, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#00FF00", BrushSize = 1.0, GeometryPath = "M 0 0 L 2 2" }
        });

        var result = _service.ListFilesWithInk(_tempDir);
        result.Should().HaveCount(2);
        result.Should().Contain(file1);
        result.Should().Contain(file2);
    }

    [Fact]
    public void ListFilesWithInk_ShouldPreserveSourceNamesContainingSidecarSuffix()
    {
        var filePath = CreateTempFile("lesson.ink.json.png");

        _service.SaveInkForFile(filePath, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        });

        var result = _service.ListFilesWithInk(_tempDir);

        result.Should().ContainSingle();
        result[0].Should().Be(filePath);
    }

    [Fact]
    public void CleanupOrphanSidecarsInDirectory_ShouldPreserveSourceNamesContainingSidecarSuffix()
    {
        var filePath = CreateTempFile("lesson.ink.json.png");

        _service.SaveInkForFile(filePath, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        });

        var jsonPath = InkPersistenceService.GetJsonPath(filePath);
        var deleted = _service.CleanupOrphanSidecarsInDirectory(_tempDir);

        deleted.Should().Be(0);
        File.Exists(jsonPath).Should().BeTrue();
    }

    [Fact]
    public void HasInk_ShouldReturnFalse_WhenSidecarHasNoStrokes()
    {
        var filePath = CreateTempFile("empty.png");
        var jsonPath = InkPersistenceService.GetJsonPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(
            jsonPath,
            """
            {
              "version": 1,
              "sourcePath": "empty.png",
              "pages": [
                {
                  "pageIndex": 1,
                  "strokes": []
                }
              ]
            }
            """);

        _service.HasInkForFile(filePath).Should().BeFalse();
    }

    [Fact]
    public void ListFilesWithInk_ShouldIgnoreSidecarsWithoutValidStrokes()
    {
        var valid = CreateTempFile("valid.png");
        var empty = CreateTempFile("empty.jpg");
        var corrupted = CreateTempFile("broken.bmp");

        _service.SaveInkForFile(valid, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#123456", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        });

        var emptyJson = InkPersistenceService.GetJsonPath(empty);
        Directory.CreateDirectory(Path.GetDirectoryName(emptyJson)!);
        File.WriteAllText(
            emptyJson,
            """
            {
              "version": 1,
              "sourcePath": "empty.jpg",
              "pages": []
            }
            """);

        var corruptedJson = InkPersistenceService.GetJsonPath(corrupted);
        Directory.CreateDirectory(Path.GetDirectoryName(corruptedJson)!);
        File.WriteAllText(corruptedJson, "{ invalid json");

        var result = _service.ListFilesWithInk(_tempDir);

        result.Should().ContainSingle();
        result.Should().Contain(valid);
    }

    [Fact]
    public void CleanupOrphanSidecarsInDirectory_ShouldDeleteOnlyOrphanFiles()
    {
        var exists = CreateTempFile("exists.jpg");
        var orphan = Path.Combine(_tempDir, "orphan.jpg");
        var existsJson = InkPersistenceService.GetJsonPath(exists);
        var orphanJson = InkPersistenceService.GetJsonPath(orphan);
        Directory.CreateDirectory(Path.GetDirectoryName(existsJson)!);

        File.WriteAllText(
            existsJson,
            """
            {
              "sourcePath": "exists.jpg",
              "pages": [
                { "pageIndex": 1, "strokes": [{ "type": "Brush", "geometryPath": "M0,0 L1,1", "colorHex": "#FF0000", "opacity": 255, "brushSize": 3 }] }
              ]
            }
            """);
        File.WriteAllText(
            orphanJson,
            """
            {
              "sourcePath": "orphan.jpg",
              "pages": [
                { "pageIndex": 1, "strokes": [{ "type": "Brush", "geometryPath": "M0,0 L1,1", "colorHex": "#FF0000", "opacity": 255, "brushSize": 3 }] }
              ]
            }
            """);

        var deleted = _service.CleanupOrphanSidecarsInDirectory(_tempDir);

        deleted.Should().Be(1);
        File.Exists(existsJson).Should().BeTrue();
        File.Exists(orphanJson).Should().BeFalse();
    }

    [Fact]
    public void LoadInk_ShouldReturnNull_WhenNoSidecar()
    {
        var filePath = CreateTempFile();
        _service.LoadInkForFile(filePath).Should().BeNull();
    }

    [Fact]
    public void LoadInk_ShouldReturnNull_WhenJsonCorrupted()
    {
        var filePath = CreateTempFile();
        var jsonPath = InkPersistenceService.GetJsonPath(filePath);
        Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
        File.WriteAllText(jsonPath, "{ invalid json !!!");

        _service.LoadInkForFile(filePath).Should().BeNull();
    }

    [Fact]
    public void GetJsonPath_ShouldBuildCorrectPath()
    {
        var sourcePath = Path.Combine(_tempDir, "lecture.pdf");
        var expected = Path.Combine(_tempDir, ".ctk-ink", "lecture.pdf.ink.json");
        InkPersistenceService.GetJsonPath(sourcePath).Should().Be(expected);
    }

    [Fact]
    public void SaveInk_ShouldCreateHiddenFolder()
    {
        var filePath = CreateTempFile();
        var strokes = new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        };

        _service.SaveInkForFile(filePath, 1, strokes);

        var inkFolder = Path.Combine(_tempDir, ".ctk-ink");
        Directory.Exists(inkFolder).Should().BeTrue();
        var dirInfo = new DirectoryInfo(inkFolder);
        (dirInfo.Attributes & FileAttributes.Hidden).Should().Be(FileAttributes.Hidden);
    }

    [Fact]
    public void ListAndCleanup_ShouldHandleInvalidDirectoryPath()
    {
        var service = new InkPersistenceService();
        var invalidPath = "\0invalid-dir";

        service.ListFilesWithInk(invalidPath).Should().BeEmpty();
        service.CleanupOrphanSidecarsInDirectory(invalidPath).Should().Be(0);
    }

    [Fact]
    public void PublicApis_ShouldIgnoreInvalidSourcePath()
    {
        var service = new InkPersistenceService();
        var invalidPath = "\0invalid-source";

        var saveAct = () => service.SaveInkForFile(invalidPath, 1, new List<InkStrokeData>
        {
            new() { ColorHex = "#FF0000", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
        });
        var saveDocumentAct = () => service.SaveDocument(invalidPath, new InkDocumentData
        {
            Pages =
            {
                new InkPageData
                {
                    PageIndex = 1,
                    Strokes =
                    {
                        new InkStrokeData { ColorHex = "#FF0000", BrushSize = 1.0, GeometryPath = "M 0 0 L 1 1" }
                    }
                }
            }
        });
        var deleteAct = () => service.DeleteInkForFile(invalidPath);

        saveAct.Should().NotThrow();
        saveDocumentAct.Should().NotThrow();
        deleteAct.Should().NotThrow();
        service.LoadInkForFile(invalidPath).Should().BeNull();
        service.LoadInkPageForFile(invalidPath, 1).Should().BeNull();
        service.HasInkForFile(invalidPath).Should().BeFalse();
    }

    [Fact]
    public void Persistence_ShouldUseIgnoreInaccessibleEnumerationOptions_ForSidecarScans()
    {
        var source = File.ReadAllText(GetPersistenceSourcePath());

        source.Should().Contain("IgnoreInaccessible = true");
        source.Should().Contain("Directory.EnumerateFiles(inkFolder, \"*.ink.json\", TopLevelIgnoreInaccessibleOptions)");
    }

    private static string GetPersistenceSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkPersistenceService.cs");
    }
}
