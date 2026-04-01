using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayClearAllCrossPageRecoveryContractTests
{
    [Fact]
    public void ClearPhotoInkStateAfterClearAll_ShouldCancelPendingAutoSaveBeforePersistingEmptySnapshot()
    {
        var source = File.ReadAllText(GetOverlaySourcePath());

        source.Should().Contain("_inkSidecarAutoSaveTimer?.Stop();");
        source.Should().Contain("_inkSidecarAutoSaveGate.NextGeneration();");
        source.Should().Contain("PersistInkHistorySnapshot(sourcePath, pageIndex, new List<InkStrokeData>(), _inkPersistence);");
    }

    [Fact]
    public void ClearAll_ShouldAlwaysClearInMemoryInkStrokesBeforeNotify()
    {
        var source = File.ReadAllText(GetOverlaySourcePath());

        source.Should().Contain("if (_inkStrokes.Count > 0)");
        source.Should().Contain("_inkStrokes.Clear();");
        source.Should().Contain("NotifyInkStateChanged(updateActiveSnapshot: true);");
    }

    [Fact]
    public void RequestPhotoTransformInkRedraw_ShouldUseUnifiedRuntimeEmptyGuard()
    {
        var source = File.ReadAllText(GetTransformSourcePath());

        source.Should().Contain("TryEnforceRuntimeEmptyGuardForCurrentPage()");
        source.Should().Contain("RequestInkRedraw();");
    }

    [Fact]
    public void TryApplyNeighborInkBitmapForCurrentPage_ShouldUseUnifiedRuntimeEmptyGuard()
    {
        var source = File.ReadAllText(GetPhotoSourcePath());

        source.Should().Contain("TryEnforceRuntimeEmptyGuardForCurrentPage()");
    }

    [Fact]
    public void CrossPageNeighborPipelines_ShouldUseUnifiedRuntimeEmptyGuard()
    {
        var source = File.ReadAllText(GetCrossPageSourcePath());

        source.Should().Contain("private bool TryEnforceRuntimeEmptyGuardForCrossPageIndex(");
        source.Should().Contain("TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, knownCacheKey: cacheKey)");
        source.Should().Contain("TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, visibleNeighborSlotIndex: i)");
    }

    private static string GetOverlaySourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.xaml.cs");
    }

    private static string GetTransformSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform.cs");
    }

    private static string GetPhotoSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.cs");
    }

    private static string GetCrossPageSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage.cs");
    }
}
