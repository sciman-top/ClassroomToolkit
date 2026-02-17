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

    private void LoadCurrentPageIfExists()
    {
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        if (!_inkCacheEnabled)
        {
            ClearInkSurfaceState();
            return;
        }
        if (string.IsNullOrWhiteSpace(_currentCacheKey))
        {
            return;
        }
        if (_photoCache.TryGet(_currentCacheKey, out var cached))
        {
            System.Diagnostics.Debug.WriteLine($"[InkCache] Loaded {cached.Count} strokes for key={_currentCacheKey}");
            ApplyInkStrokes(cached);
            return;
        }
        ClearInkSurfaceState();
    }

    private void ApplyInkStrokes(IReadOnlyList<InkStrokeData> strokes)
    {
        var applySw = Stopwatch.StartNew();
        _inkStrokes.Clear();
        _inkStrokes.AddRange(CloneInkStrokes(strokes));
        RedrawInkSurface();
        _inkCacheDirty = false;
        _perfApplyStrokes.Add(applySw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void MarkInkCacheDirty()
    {
        _inkCacheDirty = true;
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
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
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

    private sealed record InkBitmapCacheEntry(List<InkStrokeData> Strokes, BitmapSource Bitmap);
    


    public bool IsWhiteboardActive => IsBoardActive();
    public bool IsPresentationFullscreenActive => _presentationFullscreenActive;
}
