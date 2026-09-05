using ClassroomToolkit.App.Ink;
using FluentAssertions;
using System.Globalization;

namespace ClassroomToolkit.Tests;

public sealed class InkStorageServiceTests
{
    [Fact]
    public void SavePage_AndLoadPage_ShouldRoundTrip()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink");
        try
        {
            var service = new InkStorageService(rootPath);
            var today = DateTime.Today;
            var page = new InkPageData
            {
                PageIndex = 1,
                DocumentName = "doc-a",
                SourcePath = "src.pptx",
                BackgroundImageFile = "bg.png"
            };

            service.SavePage(today, page);
            var loaded = service.LoadPage(today, "doc-a", 1);

            loaded.Should().NotBeNull();
            loaded!.DocumentName.Should().Be("doc-a");
            loaded.PageIndex.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void SavePage_ShouldNotLeaveTempFile_WhenTargetIsLocked()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_lock");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = DateTime.Today;
            var jsonPath = service.GetPageJsonPath(date, "doc-lock", 1);
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            File.WriteAllText(jsonPath, "{\"documentName\":\"old\"}");

            using var lockStream = new FileStream(jsonPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var page = new InkPageData { PageIndex = 1, DocumentName = "doc-lock" };

            Action act = () => service.SavePage(date, page);

            act.Should().Throw<IOException>();
            var tempFiles = Directory.GetFiles(
                Path.GetDirectoryName(jsonPath)!,
                $"{Path.GetFileName(jsonPath)}.*.tmp");
            tempFiles.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadPage_ShouldReturnNull_WhenJsonIsCorrupted()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_bad");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = DateTime.Today;
            var jsonPath = service.GetPageJsonPath(date, "doc-bad", 2);
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            File.WriteAllText(jsonPath, "{not-json");

            var loaded = service.LoadPage(date, "doc-bad", 2);

            loaded.Should().BeNull();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void LoadPage_ShouldNormalizeStructurallyValidNullCollections()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_null_collections");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = DateTime.Today;
            var jsonPath = service.GetPageJsonPath(date, "doc-null", 1);
            Directory.CreateDirectory(Path.GetDirectoryName(jsonPath)!);
            File.WriteAllText(
                jsonPath,
                """
                {
                  "pageIndex": 1,
                  "documentName": "doc-null",
                  "strokes": [
                    null,
                    {
                      "geometryPath": "M 0 0 L 10 10",
                      "ribbons": [null],
                      "blooms": [null]
                    }
                  ]
                }
                """);

            var page = service.LoadPage(date, "doc-null", 1);

            page.Should().NotBeNull();
            page!.Strokes.Should().ContainSingle();
            page.Strokes[0].Ribbons.Should().BeEmpty();
            page.Strokes[0].Blooms.Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ListPages_ShouldSkipCorruptedFiles_AndReturnValidPages()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_list");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = DateTime.Today;
            service.SavePage(date, new InkPageData { PageIndex = 1, DocumentName = "doc-list" });

            var badPath = service.GetPageJsonPath(date, "doc-list", 2);
            Directory.CreateDirectory(Path.GetDirectoryName(badPath)!);
            File.WriteAllText(badPath, "{broken");

            var pages = service.ListPages(date, "doc-list");

            pages.Should().HaveCount(1);
            pages[0].PageIndex.Should().Be(1);
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ListApis_ShouldReturnEmpty_WhenRootPathIsInvalid()
    {
        var service = new InkStorageService("\0invalid-root");

        service.ListDates().Should().BeEmpty();
        service.ListDocuments(DateTime.Today).Should().BeEmpty();
        service.ListPages(DateTime.Today, "doc").Should().BeEmpty();
    }

    [Fact]
    public void LoadPage_ShouldReturnNull_WhenRootPathIsInvalid()
    {
        var service = new InkStorageService("\0invalid-root");

        service.LoadPage(DateTime.Today, "doc", 1).Should().BeNull();
    }

    [Theory]
    [InlineData(".")]
    [InlineData("..")]
    public void GetPageJsonPath_ShouldFallback_WhenDocumentNameIsDotSegment(string documentName)
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_dotsegment");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = new DateTime(2026, 4, 26);
            var path = service.GetPageJsonPath(date, documentName, 1);

            var relativePath = Path.GetRelativePath(rootPath, Path.GetFullPath(path));

            relativePath.Should().Be(Path.Combine(
                date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                "unknown",
                "pages",
                "slide_001.json"));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPageJsonPath_ShouldNotCreateFolders_WhenOnlyReadingPath()
    {
        var parentPath = TestPathHelper.CreateDirectory("ctool_ink_read_path");
        var rootPath = Path.Combine(parentPath, "ink");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = new DateTime(2026, 4, 26);

            var path = service.GetPageJsonPath(date, "doc", 1);

            File.Exists(path).Should().BeFalse();
            Directory.Exists(rootPath).Should().BeFalse();
            service.LoadPage(date, "doc", 1).Should().BeNull();
            Directory.Exists(rootPath).Should().BeFalse();
        }
        finally
        {
            if (Directory.Exists(parentPath))
            {
                Directory.Delete(parentPath, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PagePathApis_ShouldRejectNonPositivePageIndex(int pageIndex)
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_page_index");
        try
        {
            var service = new InkStorageService(rootPath);

            Action jsonPath = () => service.GetPageJsonPath(DateTime.Today, "doc", pageIndex);
            Action imagePath = () => service.GetPageImagePath(DateTime.Today, "doc", pageIndex);

            jsonPath.Should().Throw<ArgumentOutOfRangeException>();
            imagePath.Should().Throw<ArgumentOutOfRangeException>();
            Directory.GetDirectories(rootPath).Should().BeEmpty();
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void GetPageJsonPath_ShouldTrimWindowsTrailingDotsAndSpaces()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_name_normalization");
        try
        {
            var service = new InkStorageService(rootPath);
            var date = new DateTime(2026, 4, 26);
            var path = service.GetPageJsonPath(date, "lesson. ", 1);

            Path.GetRelativePath(rootPath, path).Should().Be(Path.Combine(
                date.ToString("yyyyMMdd", CultureInfo.InvariantCulture),
                "lesson",
                "pages",
                "slide_001.json"));
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void CopyPhoto_ShouldThrowClearError_WhenAllDeterministicNamesAreOccupied()
    {
        var rootPath = TestPathHelper.CreateDirectory("ctool_ink_photo_collision");
        var photoRootPath = Path.Combine(rootPath, "photos");
        var sourcePath = Path.Combine(rootPath, "photo.png");
        var date = new DateTime(2026, 4, 26);
        var dateFolder = Path.Combine(photoRootPath, date.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        try
        {
            File.WriteAllText(sourcePath, "source");
            Directory.CreateDirectory(dateFolder);
            File.WriteAllText(Path.Combine(dateFolder, "photo.png"), "existing");
            for (var suffix = 1; suffix <= 999; suffix++)
            {
                File.WriteAllText(Path.Combine(dateFolder, $"photo_{suffix}.png"), "existing");
            }

            Action act = () => new InkStorageService(rootPath, photoRootPath).CopyPhoto(sourcePath, date);

            act.Should()
                .Throw<IOException>()
                .Which.Message.Should()
                .Contain("No available photo name remains");
        }
        finally
        {
            if (Directory.Exists(rootPath))
            {
                Directory.Delete(rootPath, recursive: true);
            }
        }
    }

    [Fact]
    public void ListApis_ShouldUseIgnoreInaccessibleEnumerationOptions()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("IgnoreInaccessible = true");
        source.Should().Contain("Directory.EnumerateDirectories(_rootPath, \"*\", TopLevelIgnoreInaccessibleOptions)");
        source.Should().Contain("Directory.EnumerateDirectories(dateFolder, \"*\", TopLevelIgnoreInaccessibleOptions)");
        source.Should().Contain("Directory.EnumerateFiles(pagesFolder, \"slide_*.json\", TopLevelIgnoreInaccessibleOptions)");
        source.Should().Contain("Directory.EnumerateDirectories(rootPath, \"*\", TopLevelIgnoreInaccessibleOptions)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkStorageService.cs");
    }
}
