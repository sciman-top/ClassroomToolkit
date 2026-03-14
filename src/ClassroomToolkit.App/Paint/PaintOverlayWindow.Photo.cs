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
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "load-enter",
                $"allowDisk={allowDiskFallback} preferFast={preferInteractiveFastPath}");
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("load-skip", "scope!=photo");
            }
            return;
        }
        if (!_inkCacheEnabled)
        {
            ClearInkSurfaceState();
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("load-clear", "cache-disabled");
            }
            return;
        }
        // If "显示笔迹" is off, don't load previously saved ink
        if (!_inkShowEnabled)
        {
            ClearInkSurfaceState();
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("load-clear", "ink-hidden");
            }
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("load-skip", "empty-cache-key");
            }
            return;
        }
        if (_photoCache.TryGet(_currentCacheKey, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[InkCache] Loaded {cached.Count} strokes for key={_currentCacheKey}");
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("load-cache-hit", $"strokes={cached.Count}");
            }
            ApplyInkStrokes(cached, preferInteractiveFastPath);
            return;
        }
        // Method A fallback: only load persisted ink when save-toggle allows persistence.
        if (InkPersistenceTogglePolicy.ShouldLoadPersistedInk(allowDiskFallback, _inkSaveEnabled)
            && _inkPersistence != null
            && TryLoadInkFromSidecar())
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("load-sidecar-hit");
            }
            return;
        }
        ClearInkSurfaceState();
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("load-clear", "cache-miss");
        }
    }

    private void ApplyInkStrokes(IReadOnlyList<InkStrokeData> strokes, bool preferInteractiveFastPath = false)
    {
        var applySw = Stopwatch.StartNew();
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "apply-enter",
                $"strokes={strokes.Count} preferFast={preferInteractiveFastPath}");
        }
        _inkStrokes.Clear();
        if (preferInteractiveFastPath)
        {
            _inkStrokes.AddRange(strokes);
        }
        else
        {
            _inkStrokes.AddRange(CloneInkStrokes(strokes));
        }

        var fastApplied = TryApplyNeighborInkBitmapForCurrentPage(strokes, preferInteractiveFastPath);
        if (!fastApplied)
        {
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("apply-redraw");
            }
            RedrawInkSurface();
        }
        else if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("apply-fast-bitmap");
        }

        MarkCurrentInkPageLoaded(_inkStrokes);
        _perfApplyStrokes.Add(applySw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("apply-exit", $"ms={applySw.Elapsed.TotalMilliseconds:F2}");
        }
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
        catch
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
        catch
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
