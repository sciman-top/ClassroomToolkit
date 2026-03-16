using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayLifecycleContractTests
{
    [Fact]
    public void UpdateCrossPageDisplay_ShouldClearNeighborPages_WhenSinglePageOrInvalidCurrentFrame()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (totalPages <= 1)");
        source.Should().Contain("ClearNeighborPages();");
        source.Should().Contain("if (currentBitmap == null)");
        source.Should().Contain("if (currentPageHeight <= 0)");
    }

    [Fact]
    public void ScheduleNeighborImagePrefetch_ShouldRevalidateSequenceBounds_InsideAsyncCallback()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_photoSequencePaths.Count == 0 || pageIndex < 1 || pageIndex > _photoSequencePaths.Count");
        source.Should().Contain("var path = _photoSequencePaths[pageIndex - 1];");
    }

    [Fact]
    public void ScheduleNeighborInkRender_ShouldRejectStaleDocumentCacheKey()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var expectedCacheKey = BuildNeighborInkCacheKey(pageIndex);");
        source.Should().Contain("!string.Equals(cacheKey, expectedCacheKey, StringComparison.Ordinal)");
    }

    private static string GetSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
