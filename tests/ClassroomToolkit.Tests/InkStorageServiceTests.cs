using ClassroomToolkit.App.Ink;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkStorageServiceTests
{
    [Fact]
    public void SavePage_AndLoadPage_ShouldRoundTrip()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_ink_{Guid.NewGuid():N}");
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
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_ink_lock_{Guid.NewGuid():N}");
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
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_ink_bad_{Guid.NewGuid():N}");
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
    public void ListPages_ShouldSkipCorruptedFiles_AndReturnValidPages()
    {
        var rootPath = Path.Combine(Path.GetTempPath(), $"ctool_ink_list_{Guid.NewGuid():N}");
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
}
