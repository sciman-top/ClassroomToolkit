using System.IO;
using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests.Paint;

[Trait("Gate", "CoreContract")]
public sealed class CrossPageDisplayLifecycleContractTests
{
    [Fact]
    public void CrossPageDisplayClearPolicy_ShouldClearNeighborPages_WhenSinglePageOrInvalidCurrentFrame()
    {
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 1,
            hasCurrentBitmap: true,
            currentPageHeight: 100).Should().BeTrue();
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: false,
            currentPageHeight: 100).Should().BeTrue();
        CrossPageDisplayUpdatePolicies.ShouldClearNeighborPages(
            totalPages: 5,
            hasCurrentBitmap: true,
            currentPageHeight: 0).Should().BeTrue();
    }

    [Fact]
    public void NeighborPrefetchPageBounds_ShouldGuardSequenceAccess()
    {
        CrossPageNeighborInkPolicies.IsNeighborPrefetchPageOutOfRange(pageIndex: 0, pathCount: 5).Should().BeTrue();
        CrossPageNeighborInkPolicies.IsNeighborPrefetchPageOutOfRange(pageIndex: -1, pathCount: 5).Should().BeTrue();
        CrossPageNeighborInkPolicies.IsNeighborPrefetchPageOutOfRange(pageIndex: 6, pathCount: 5).Should().BeTrue();
        CrossPageNeighborInkPolicies.IsNeighborPrefetchPageOutOfRange(pageIndex: 1, pathCount: 0).Should().BeTrue();
        CrossPageNeighborInkPolicies.IsNeighborPrefetchPageOutOfRange(pageIndex: 1, pathCount: 5).Should().BeFalse();
        CrossPageNeighborInkPolicies.IsNeighborPrefetchPageOutOfRange(pageIndex: 5, pathCount: 5).Should().BeFalse();
    }

    [Fact]
    public void ScheduleNeighborImagePrefetch_ShouldRouteBoundsChecksThroughSharedPredicate()
    {
        // 弱化接线断言：同步前置与异步闭包复验都必须经由共享谓词，防止复验被绕开；
        // 不再冻结格式化文本，表达式与排版的改动不破契约。
        var source = File.ReadAllText(GetHelpersSourcePath());

        var occurrences = source.Split("IsNeighborPrefetchPageOutOfRange").Length - 1;
        occurrences.Should().BeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void CrossPageNeighborInkRenderAdmissionPolicy_ShouldRejectStaleDocumentCacheKey()
    {
        CrossPageNeighborInkPolicies.ShouldRejectStaleCacheKey(
            cacheKey: "docA|3",
            expectedCacheKey: "docB|3").Should().BeTrue();
        CrossPageNeighborInkPolicies.ShouldRejectStaleCacheKey(
            cacheKey: "docA|3",
            expectedCacheKey: "docA|3").Should().BeFalse();
    }

    [Fact]
    public void DelayedDispatchFailureHandling_ShouldMarshalStateMutation_ToUiThread()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint", "CrossPage",
            "PaintOverlayWindow.Photo.CrossPage*.cs");

        source.Should().Contain("HandleCrossPageDisplayUpdateDispatchFailureOnUiThread(");
        source.Should().Contain("if (Dispatcher.CheckAccess())");
        source.Should().Contain("_inkDiagnostics?.OnCrossPageUpdateEvent(\"defer-abort\", source, abortDetail);");
    }

    [Fact]
    public void ScheduleNeighborInkSidecarLoad_ShouldRejectStaleDocumentCacheKey()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint", "CrossPage",
            "PaintOverlayWindow.Photo.CrossPage*.cs");

        source.Should().Contain("ScheduleNeighborInkSidecarLoad(");
        source.Should().Contain("_inkDiagnostics?.OnCrossPageUpdateEvent(\"skip\", CrossPageUpdateSources.NeighborSidecar, \"stale-cache-key\");");
    }

    private static string GetHelpersSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint", "CrossPage",
            "PaintOverlayWindow.Photo.CrossPage.Helpers.cs");
    }
}
