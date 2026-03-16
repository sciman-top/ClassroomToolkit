using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void ExecutePhotoClose()
    {
        PhotoCloseRequested?.Invoke();
        ExitPhotoMode();
    }

    private void ResetInkHistory()
    {
        _history.Clear();
        _inkHistory.Clear();
    }

    private void LoadCurrentPageIfExists(bool allowDiskFallback = true, bool preferInteractiveFastPath = false)
    {
        var result = InkPageLoadCoordinator.Apply(
            photoCacheScopeActive: _currentCacheScope == InkCacheScope.Photo,
            inkCacheEnabled: _inkCacheEnabled,
            inkShowEnabled: _inkShowEnabled,
            currentCacheKey: _currentCacheKey,
            allowDiskFallback: allowDiskFallback,
            hasInkPersistence: _inkPersistence != null,
            preferInteractiveFastPath: preferInteractiveFastPath,
            tryGetCachedStrokes: (string cacheKey, out List<InkStrokeData> strokes) => _photoCache.TryGet(cacheKey, out strokes),
            tryLoadInkFromSidecar: TryLoadInkFromSidecar,
            purgePersistedInkForHiddenCurrentPage: PurgePersistedInkForHiddenCurrentPageIfNeeded,
            clearInkSurfaceState: ClearInkSurfaceState,
            applyInkStrokes: ApplyInkStrokes,
            markTraceStage: IsCrossPageFirstInputTraceActive()
                ? (stage, detail) => MarkCrossPageFirstInputStage(stage, detail)
                : null);

        if (result.AppliedCachedStrokes)
        {
            System.Diagnostics.Debug.WriteLine($"[InkCache] Loaded {result.LoadedStrokeCount} strokes for key={_currentCacheKey}");
        }
    }

    private void PurgePersistedInkForHiddenCurrentDocumentIfNeeded()
    {
        PurgePersistedInkForHiddenSourceIfNeeded(_currentDocumentPath);
    }

    private void PurgePersistedInkForHiddenCurrentPageIfNeeded()
    {
        PurgePersistedInkForHiddenPageIfNeeded(_currentDocumentPath, _currentPageIndex);
    }

    private void PurgePersistedInkForHiddenSourceIfNeeded(string sourcePath)
    {
        if (!_inkSaveEnabled || _inkShowEnabled || _inkPersistence == null || string.IsNullOrWhiteSpace(sourcePath))
        {
            return;
        }

        try
        {
            var inkDoc = _inkPersistence.LoadInkForFile(sourcePath);
            if (inkDoc?.Pages == null || inkDoc.Pages.Count == 0)
            {
                return;
            }

            var removedCount = 0;
            var keptCount = 0;
            foreach (var page in inkDoc.Pages.ToList())
            {
                if (PurgePersistedInkForHiddenPageIfNeeded(sourcePath, page.PageIndex))
                {
                    removedCount++;
                }
                else
                {
                    keptCount++;
                }
            }
            System.Diagnostics.Debug.WriteLine(
                $"[InkPersist] Hidden-source purge summary: source={sourcePath}, removed={removedCount}, kept={keptCount}");
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"[InkPersist] Hidden-source purge failed: source={sourcePath}, error={ex.Message}");
        }
    }

    private bool PurgePersistedInkForHiddenPageIfNeeded(string sourcePath, int pageIndex)
    {
        if (!_inkSaveEnabled || _inkShowEnabled || _inkPersistence == null || string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return false;
        }

        if (WasPageModifiedInSession(sourcePath, pageIndex))
        {
            return false;
        }

        try
        {
            var existing = _inkPersistence.LoadInkPageForFile(sourcePath, pageIndex);
            if (existing == null || existing.Count == 0)
            {
                return false;
            }

            _inkPersistence.SaveInkForFile(sourcePath, pageIndex, new List<InkStrokeData>());
            _inkExport?.RemoveCompositeOutputsForPage(sourcePath, pageIndex);
            MarkInkPageLoaded(sourcePath, pageIndex, Array.Empty<InkStrokeData>());

            var cacheKey = BuildPhotoModeCacheKey(sourcePath, pageIndex, IsPdfFile(sourcePath));
            if (!string.IsNullOrWhiteSpace(cacheKey))
            {
                _photoCache.Remove(cacheKey);
                InvalidateNeighborInkCache(cacheKey);
            }

            System.Diagnostics.Debug.WriteLine($"[InkPersist] Hidden-page purge: source={sourcePath}, page={pageIndex}");
            return true;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"[InkPersist] Hidden-page purge failed: source={sourcePath}, page={pageIndex}, error={ex.Message}");
            return false;
        }
    }

    private void ApplyInkStrokes(IReadOnlyList<InkStrokeData> strokes, bool preferInteractiveFastPath = false)
    {
        var applySw = Stopwatch.StartNew();
        InkStrokeApplyCoordinator.Apply(
            strokes: strokes,
            preferInteractiveFastPath: preferInteractiveFastPath,
            clearRuntimeStrokes: _inkStrokes.Clear,
            addRuntimeStrokes: runtimeStrokes =>
            {
                if (preferInteractiveFastPath)
                {
                    _inkStrokes.AddRange(runtimeStrokes);
                }
                else
                {
                    _inkStrokes.AddRange(CloneInkStrokes(runtimeStrokes));
                }
            },
            tryApplyNeighborInkBitmapForCurrentPage: TryApplyNeighborInkBitmapForCurrentPage,
            redrawInkSurface: RedrawInkSurface,
            finalizeFastAppliedInkSurface: FinalizeFastAppliedInkSurface,
            markCurrentInkPageLoaded: () => MarkCurrentInkPageLoaded(_inkStrokes),
            recordPerfMilliseconds: (elapsedMs, onDispatcher) => _perfApplyStrokes.Add(elapsedMs, onDispatcher),
            getElapsedMilliseconds: () => applySw.Elapsed.TotalMilliseconds,
            dispatcherCheckAccess: Dispatcher.CheckAccess,
            markTraceStage: IsCrossPageFirstInputTraceActive()
                ? (stage, detail) => MarkCrossPageFirstInputStage(stage, detail)
                : null);
    }

    private void FinalizeFastAppliedInkSurface()
    {
        _lastInkRedrawUtc = GetCurrentUtcTimestamp();
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: IsPhotoInkModeActive());
        OnInkRedrawCompleted();
    }

    private bool TryApplyNeighborInkBitmapForCurrentPage(IReadOnlyList<InkStrokeData> strokes, bool interactiveSwitch)
    {
        if (!interactiveSwitch || !_photoModeActive || strokes.Count == 0)
        {
            return false;
        }

        var currentPage = GetCurrentPageIndexForCrossPage();
        var cacheKey = BuildNeighborInkCacheKey(currentPage);
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return false;
        }

        if (!_neighborInkCache.TryGetValue(cacheKey, out var neighborEntry))
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("apply-fast-skip", "cache-miss");
            }
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", "interactive-fastpath", "cache-miss");
            return false;
        }

        var bitmap = neighborEntry.Bitmap;
        var decision = CrossPageInkFastPathSelector.EvaluateCandidateForRasterCopy(
            interactiveSwitch,
            strokes,
            neighborEntry.Strokes,
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            bitmap.DpiX,
            bitmap.DpiY,
            _surfacePixelWidth,
            _surfacePixelHeight,
            _surfaceDpiX,
            _surfaceDpiY);
        if (!decision.ShouldApply)
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("apply-fast-skip", decision.Reason);
            }
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", "interactive-fastpath", decision.Reason);
            return false;
        }
        if (!TryCopyBitmapToRasterSurface(bitmap))
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("apply-fast-skip", "copy-failed");
            }
            _inkDiagnostics?.OnCrossPageUpdateEvent("skip", "interactive-fastpath", "copy-failed");
            return false;
        }

        _hasDrawing = true;
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("apply-fast-hit", "source=neighbor-cache");
        }
        _inkDiagnostics?.OnCrossPageUpdateEvent("apply", "interactive-fastpath", "source=neighbor-cache");
        return true;
    }

    private bool TryCopyBitmapToRasterSurface(BitmapSource source)
    {
        if (_rasterSurface == null)
        {
            return false;
        }

        var converted = source.Format == PixelFormats.Pbgra32
            ? source
            : new FormatConvertedBitmap(source, PixelFormats.Pbgra32, null, 0);
        if (converted is Freezable freezable && freezable.CanFreeze && !freezable.IsFrozen)
        {
            freezable.Freeze();
        }

        var width = Math.Min(converted.PixelWidth, _surfacePixelWidth);
        var height = Math.Min(converted.PixelHeight, _surfacePixelHeight);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        ClearSurface();
        var stride = width * 4;
        var pixels = new byte[stride * height];
        converted.CopyPixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        _rasterSurface.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        return true;
    }

    private void FinalizeActiveInkOperation()
    {
        if (_lastPointerPosition == null)
        {
            return;
        }
        var position = _lastPointerPosition.Value;
        if (_strokeInProgress)
        {
            EndBrushStroke(position);
        }
        else if (_isErasing)
        {
            EndEraser(position);
        }
        else if (_isRegionSelecting)
        {
            EndRegionSelection(position);
        }
        else if (_isDrawingShape)
        {
            EndShape(position);
        }
        ReleasePointerInput();
    }

    private static string BuildPhotoCacheKey(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return string.Empty;
        }
        try
        {
            return $"img|{IoPath.GetFullPath(sourcePath)}";
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            return $"img|{sourcePath}";
        }
    }

    private string BuildPhotoModeCacheKey(string sourcePath, int pageIndex, bool isPdf)
    {
        if (!isPdf)
        {
            return BuildPhotoCacheKey(sourcePath);
        }
        return BuildPdfCacheKey(sourcePath, pageIndex);
    }

    private static string BuildPdfCacheKey(string sourcePath, int pageIndex)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return string.Empty;
        }
        try
        {
            return $"pdf|{IoPath.GetFullPath(sourcePath)}|page_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            return $"pdf|{sourcePath}|page_{pageIndex.ToString("D3", CultureInfo.InvariantCulture)}";
        }
    }

    private static bool IsPdfFile(string path)
    {
        var ext = IoPath.GetExtension(path);
        return !string.IsNullOrWhiteSpace(ext) && ext.Equals(".pdf", StringComparison.OrdinalIgnoreCase);
    }
    
    private string BuildNeighborInkCacheKey(int pageIndex)
    {
        if (_photoDocumentIsPdf)
        {
            return BuildPdfCacheKey(_currentDocumentPath, pageIndex);
        }
        var arrayIndex = pageIndex - 1;
        if (arrayIndex < 0 || arrayIndex >= _photoSequencePaths.Count)
        {
            return string.Empty;
        }
        return BuildPhotoCacheKey(_photoSequencePaths[arrayIndex]);
    }

    private sealed record InkBitmapCacheEntry(int PageIndex, List<InkStrokeData> Strokes, BitmapSource Bitmap);
    


    public bool IsWhiteboardActive => IsBoardActive();
    public bool IsPresentationFullscreenActive => _presentationFullscreenActive;
}


