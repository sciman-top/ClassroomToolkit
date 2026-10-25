using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Utilities;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private static long EstimateExportWorkload(string sourcePath, InkDocumentData? inkDoc)
    {
        var hasInkPages = inkDoc?.Pages?.Count(page => page.Strokes?.Count > 0) ?? 0;
        var pageWeight = Math.Max(1, hasInkPages);
        var sizeWeight = SafeActionExecutionExecutor.TryExecute(
            () =>
            {
                if (!string.IsNullOrWhiteSpace(sourcePath) && File.Exists(sourcePath))
                {
                    return Math.Max(1, new FileInfo(sourcePath).Length / (512 * 1024));
                }

                return 1L;
            },
            fallback: 1L,
            onFailure: ex => System.Diagnostics.Debug.WriteLine(
                $"[InkExport] Estimate workload fallback for '{sourcePath}': {ex.GetType().Name} - {ex.Message}"));
        return pageWeight * sizeWeight;
    }

    private InkDocumentData? BuildInkDocumentForExport(string sourceFilePath)
    {
        if (_inkPersistence == null || string.IsNullOrWhiteSpace(sourceFilePath))
        {
            return null;
        }

        PurgePersistedInkForHiddenSourceIfNeeded(sourceFilePath);

        var inkDoc = _inkPersistence.LoadInkForFile(sourceFilePath) ?? new InkDocumentData
        {
            SourcePath = sourceFilePath
        };

        var includeRuntimeCachePages = !_inkSaveEnabled;
        if (includeRuntimeCachePages)
        {
            InkExportSnapshotBuilder.MergeCachedPages(inkDoc, sourceFilePath, _photoCache.Snapshot(), CloneInkStrokes);
        }

        if (!string.Equals(sourceFilePath, _currentDocumentPath, StringComparison.OrdinalIgnoreCase))
        {
            ApplyExportScopeFilter(inkDoc);
            return inkDoc;
        }

        BackfillStrokeReferenceSizeForCurrentImage(inkDoc);
        if (includeRuntimeCachePages || IsPageDirtyForExportOverlay(sourceFilePath, _currentPageIndex))
        {
            FinalizeActiveInkOperation();
            InkExportSnapshotBuilder.UpsertPageStrokes(
                inkDoc,
                sourceFilePath,
                _currentPageIndex,
                CloneCommittedInkStrokes());
        }
        ApplyExportScopeFilter(inkDoc);
        return inkDoc;
    }

    private bool IsPageDirtyForExportOverlay(string sourcePath, int pageIndex)
    {
        return _inkDirtyPages.IsDirty(sourcePath, pageIndex);
    }

    private void LogExportAudit(string sourcePath, InkDocumentData? inkDoc)
    {
        if (inkDoc?.Pages == null || inkDoc.Pages.Count == 0)
        {
            return;
        }

        foreach (var page in inkDoc.Pages.Where(p => p.PageIndex > 0))
        {
            var hash = ComputeInkHash(page.Strokes ?? new List<InkStrokeData>());
            var dirty = _inkDirtyPages.IsDirty(sourcePath, page.PageIndex);
            _inkDirtyPages.TryGetRuntimeState(sourcePath, page.PageIndex, out var version, out var lastKnownHash, out _);
            System.Diagnostics.Debug.WriteLine(
                $"[InkExportAudit] source={sourcePath}, page={page.PageIndex}, version={version}, dirty={dirty}, expectedHash={hash}, runtimeHash={lastKnownHash}");
        }
    }

    private bool TryGetRuntimePageStrokesForPersist(string sourcePath, int pageIndex, out List<InkStrokeData> strokes)
    {
        strokes = new List<InkStrokeData>();
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        if (string.Equals(sourcePath, _currentDocumentPath, StringComparison.OrdinalIgnoreCase) && pageIndex == _currentPageIndex)
        {
            strokes = CloneCommittedInkStrokes();
            return true;
        }

        var cacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        if (_photoCache.TryGet(cacheKey, out var cached))
        {
            strokes = CloneInkStrokes(cached);
            return true;
        }

        return false;
    }

    private void BackfillStrokeReferenceSizeForCurrentImage(InkDocumentData inkDoc)
    {
        if (_photoDocumentIsPdf || PhotoBackground.Source is not System.Windows.Media.Imaging.BitmapSource bitmap)
        {
            return;
        }

        var width = GetBitmapDisplayWidthInDip(bitmap);
        var height = GetBitmapDisplayHeightInDip(bitmap);
        if (width <= 0.5 || height <= 0.5)
        {
            return;
        }

        foreach (var page in inkDoc.Pages)
        {
            foreach (var stroke in page.Strokes)
            {
                if (stroke.ReferenceWidth > 0.5 && stroke.ReferenceHeight > 0.5)
                {
                    continue;
                }
                stroke.ReferenceWidth = width;
                stroke.ReferenceHeight = height;
            }
        }
    }

    private void ApplyExportScopeFilter(InkDocumentData inkDoc)
    {
        InkExportSnapshotBuilder.ApplyScopeFilter(
            inkDoc,
            _inkExportOptions.Scope,
            page => WasPageModifiedInSession(inkDoc.SourcePath, page.PageIndex));
    }

    private IEnumerable<string> EnumerateCachedInkSourcesInDirectory(string directoryPath)
    {
        return InkExportSnapshotBuilder.EnumerateCachedInkSourcesInDirectory(_photoCache.Snapshot(), directoryPath);
    }

    private List<string> CollectDirectoryExportCandidates(string directoryPath)
    {
        var filesToExportSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (_inkExportOptions.Scope == InkExportScope.SessionChangesOnly)
        {
            foreach (var sourcePath in EnumerateSessionModifiedSourcesInDirectory(directoryPath))
            {
                filesToExportSet.Add(sourcePath);
            }
        }
        else
        {
            foreach (var file in _inkPersistence!.ListFilesWithInk(directoryPath))
            {
                filesToExportSet.Add(file);
            }
            foreach (var file in _inkExport!.ListFilesWithCompositeExports(directoryPath))
            {
                filesToExportSet.Add(file);
            }

            if (!_inkSaveEnabled)
            {
                foreach (var sourcePath in EnumerateCachedInkSourcesInDirectory(directoryPath))
                {
                    filesToExportSet.Add(sourcePath);
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(_currentDocumentPath) &&
            string.Equals(Path.GetDirectoryName(_currentDocumentPath), directoryPath, StringComparison.OrdinalIgnoreCase))
        {
            var currentInkDoc = BuildInkDocumentForExport(_currentDocumentPath);
            if (InkExportSnapshotBuilder.HasAnyInkStrokes(currentInkDoc)
                || IsPageDirtyForExportOverlay(_currentDocumentPath, _currentPageIndex))
            {
                filesToExportSet.Add(_currentDocumentPath);
            }
        }

        return filesToExportSet.ToList();
    }

    private (List<string> ExportQueue, Dictionary<string, InkDocumentData?> ExportSnapshotDocs, int OverwriteCount)
        BuildDirectoryExportPlan(List<string> filesToExport)
    {
        var inkDocsForExport = new Dictionary<string, InkDocumentData?>(StringComparer.OrdinalIgnoreCase);
        var overwriteCount = 0;
        foreach (var sourcePath in filesToExport)
        {
            var inkDoc = BuildInkDocumentForExport(sourcePath);
            inkDocsForExport[sourcePath] = inkDoc;
            if (!InkExportSnapshotBuilder.HasAnyInkStrokes(inkDoc))
            {
                continue;
            }

            overwriteCount += _inkExport!.GetExistingOutputPaths(sourcePath, inkDoc, _inkExportOptions).Count;
        }

        var exportQueue = filesToExport
            .OrderByDescending(path => EstimateExportWorkload(path, inkDocsForExport.TryGetValue(path, out var doc) ? doc : null))
            .ToList();

        // Snapshot export payloads so background work is isolated from subsequent edits.
        var exportSnapshotDocs = new Dictionary<string, InkDocumentData?>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in inkDocsForExport)
        {
            exportSnapshotDocs[pair.Key] = InkExportSnapshotBuilder.CloneDocument(pair.Value, CloneInkStrokes);
        }

        return (exportQueue, exportSnapshotDocs, overwriteCount);
    }

    private static int ResolveDirectoryExportParallelism(int total, InkExportOptions exportOptions)
    {
        var adaptiveParallel = Math.Max(1, Math.Min(4, Math.Max(1, Environment.ProcessorCount / 2)));
        var configuredParallel = exportOptions.MaxParallelFiles > 0 ? exportOptions.MaxParallelFiles : adaptiveParallel;
        return Math.Max(1, Math.Min(configuredParallel, Math.Max(1, total)));
    }

    // Compatibility shim for existing reflection-based tests/utilities.
    private static bool TryParseCacheKey(string cacheKey, out string sourcePath, out int pageIndex)
    {
        return InkExportSnapshotBuilder.TryParseCacheKey(cacheKey, out sourcePath, out pageIndex);
    }
}
