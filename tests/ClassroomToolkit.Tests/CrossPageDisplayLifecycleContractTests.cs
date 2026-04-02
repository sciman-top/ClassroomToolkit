using System.IO;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDisplayLifecycleContractTests
{
    [Fact]
    public void CrossPageDisplayClearPolicy_ShouldClearNeighborPages_WhenSinglePageOrInvalidCurrentFrame()
    {
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 1,
            hasCurrentBitmap: true,
            currentPageHeight: 100).Should().BeTrue();
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: false,
            currentPageHeight: 100).Should().BeTrue();
        CrossPageDisplayClearPolicy.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: true,
            currentPageHeight: 0).Should().BeTrue();
    }

    [Fact]
    public void ScheduleNeighborImagePrefetch_ShouldRevalidateSequenceBounds_InsideAsyncCallback()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_photoSequencePaths.Count == 0 || pageIndex < 1 || pageIndex > _photoSequencePaths.Count");
        source.Should().Contain("var path = _photoSequencePaths[pageIndex - 1];");
    }

    [Fact]
    public void CrossPageNeighborInkRenderAdmissionPolicy_ShouldRejectStaleDocumentCacheKey()
    {
        CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(
            cacheKey: "docA|3",
            expectedCacheKey: "docB|3").Should().BeTrue();
        CrossPageNeighborInkRenderAdmissionPolicy.ShouldRejectStaleCacheKey(
            cacheKey: "docA|3",
            expectedCacheKey: "docA|3").Should().BeFalse();
    }

    [Fact]
    public void DelayedDispatchFailureHandling_ShouldMarshalStateMutation_ToUiThread()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("_inkDiagnostics?.OnCrossPageUpdateEvent(\"defer-abort\", source, abortDetail);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage.cs");
    }
}
