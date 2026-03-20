using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayLifecycleContractTests
{
    [Fact]
    public void UpdateCrossPageDisplay_ShouldClearNeighborPages_WhenSinglePageOrInvalidCurrentFrame()
    {
        var source = ReadCrossPageSource();

        source.Should().Contain("if (totalPages <= 1)");
        source.Should().Contain("ClearNeighborPages();");
        source.Should().Contain("if (currentBitmap == null)");
        source.Should().Contain("if (currentPageHeight <= 0)");
    }

    [Fact]
    public void ScheduleNeighborImagePrefetch_ShouldRevalidateSequenceBounds_InsideAsyncCallback()
    {
        var source = ReadCrossPageSource();

        source.Should().Contain("_photoSequencePaths.Count == 0 || pageIndex < 1 || pageIndex > _photoSequencePaths.Count");
        source.Should().Contain("var path = _photoSequencePaths[pageIndex - 1];");
    }

    [Fact]
    public void ScheduleNeighborInkRender_ShouldRejectStaleDocumentCacheKey()
    {
        var source = ReadCrossPageSource();

        source.Should().Contain("var expectedCacheKey = BuildNeighborInkCacheKey(pageIndex);");
        source.Should().Contain("!string.Equals(cacheKey, expectedCacheKey, StringComparison.Ordinal)");
    }

    [Fact]
    public void DelayedDispatchFailureHandling_ShouldMarshalStateMutation_ToUiThread()
    {
        var source = ReadCrossPageSource();

        source.Should().Contain("HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("_inkDiagnostics?.OnCrossPageUpdateEvent(\"defer-abort\", source, abortDetail);");
    }

    private static string ReadCrossPageSource()
    {
        return string.Join(
            "\n",
            ReadCrossPagePart("PaintOverlayWindow.Photo.CrossPage.cs"),
            ReadCrossPagePart("PaintOverlayWindow.Photo.CrossPage.Display.cs"),
            ReadCrossPagePart("PaintOverlayWindow.Photo.CrossPage.NeighborInk.cs"));
    }

    private static string ReadCrossPagePart(string fileName)
    {
        return File.ReadAllText(GetSourcePath(fileName));
    }

    private static string GetSourcePath(string fileName)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            fileName);
    }
}
