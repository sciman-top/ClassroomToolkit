using System;
using System.IO;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayClearAllCrossPageRecoveryContractTests
{
    [Fact]
    public void ClearPhotoInkStateAfterClearAll_ShouldCancelPendingAutoSaveBeforePersistingEmptySnapshot()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("_inkSidecarAutoSaveTimer?.Stop();");
        source.Should().Contain("_inkSidecarAutoSaveGate.NextGeneration();");
        source.Should().Contain("MarkInkPageModified(sourcePath, pageIndex, \"empty\", Array.Empty<InkStrokeData>());");
        source.Should().Contain("MarkInkPagePersistedIfUnchanged(sourcePath, pageIndex, \"empty\");");
        source.Should().Contain("var persisted = PersistInkHistorySnapshot(");
        source.Should().Contain("new List<InkStrokeData>(),");
        source.Should().Contain("if (!persisted)");
    }

    [Fact]
    public void ClearAll_ShouldAlwaysClearInMemoryInkStrokesBeforeNotify()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("if (_inkStrokes.Count > 0)");
        source.Should().Contain("_inkStrokes.Clear();");
        source.Should().Contain("NotifyInkStateChanged(updateActiveSnapshot: true);");
    }

    [Fact]
    public void RequestPhotoTransformInkRedraw_ShouldUseUnifiedRuntimeEmptyGuard()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.Transform*.cs");

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
    public void HiddenPagePurge_ShouldNotClearRuntimeAfterSidecarClearFails()
    {
        var source = File.ReadAllText(GetPhotoSourcePath());

        source.Should().Contain("if (!_inkPersistence.SaveInkForFile(sourcePath, pageIndex, new List<InkStrokeData>()))");
        source.Should().Contain("sidecar clear was not durable");
        source.Should().Contain("_inkExport?.RemoveCompositeOutputsForPage(sourcePath, pageIndex);");
    }

    [Fact]
    public void CrossPageNeighborPipelines_ShouldUseUnifiedRuntimeEmptyGuard()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage*.cs");

        source.Should().Contain("private bool TryEnforceRuntimeEmptyGuardForCrossPageIndex(");
        source.Should().Contain("TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, knownCacheKey: cacheKey)");
        source.Should().Contain("TryEnforceRuntimeEmptyGuardForCrossPageIndex(pageIndex, visibleNeighborSlotIndex: i)");
    }

    [Fact]
    public void UndoAcrossPages_ShouldRestoreRuntimeAndPersistedPhotoInkState()
    {
        var source = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow*.cs");

        source.Should().Contain("if (!TryApplyGlobalUndoSnapshot(_globalInkHistory[index]))");
        source.Should().Contain("_globalInkHistory.RemoveAt(_globalInkHistory.Count - 1);");
        source.Should().Contain("RemoveMatchingCurrentInkHistorySnapshot(snapshot, snapshotHash);");
        source.Should().Contain("PersistUndoRestoredPhotoInkSnapshot(_currentDocumentPath, _currentPageIndex, _inkStrokes);");
        source.Should().Contain("_inkSidecarAutoSaveGate.NextGeneration();");
        source.Should().Contain("PersistInkToSidecar(CloneInkStrokes(strokes), sourcePath, pageIndex);");
    }

    private static string GetPhotoSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.cs");
    }

}
