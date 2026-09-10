using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using MediaColor = System.Windows.Media.Color;
using WpfPath = System.Windows.Shapes.Path;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private sealed class RasterSnapshot : IDisposable
    {
        public RasterSnapshot(int width, int height, double dpiX, double dpiY, byte[] pixels)
        {
            PixelWidth = width;
            PixelHeight = height;
            DpiX = dpiX;
            DpiY = dpiY;
            Pixels = pixels;
        }

        public int PixelWidth { get; }
        public int PixelHeight { get; }
        public double DpiX { get; }
        public double DpiY { get; }
        public byte[] Pixels { get; }

        public void Dispose()
        {
            if (Pixels != null)
            {
                PixelPool.Return(Pixels);
            }
        }
    }

    private sealed record InkSnapshot(string SourcePath, int PageIndex, string Hash, List<InkStrokeData> Strokes);
    private sealed record GlobalInkSnapshot(string SourcePath, int PageIndex, string CacheKey, List<InkStrokeData> Strokes);

    private void PushHistory()
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }

        var trackVectorSnapshot = InkUndoHistoryPolicy.ShouldTrackVectorSnapshot(_inkRecordEnabled, IsPhotoInkModeActive());
        if (trackVectorSnapshot && HasDuplicateVectorSnapshot())
        {
            // 状态与上一条向量快照一致：原先会推入整页位图快照后再弹出，
            // 现在直接跳过，省掉一次全屏 CopyPixels 与全部笔画克隆。
            return;
        }

        var stride = _surfacePixelWidth * 4;
        var bytesRequired = stride * _surfacePixelHeight;

        // Check memory pressure and trim if needed
        while (_history.Count > 0 && (_history.Count >= HistoryLimit || _currentHistoryMemoryBytes + bytesRequired > MaxHistoryMemoryBytes))
        {
            var oldest = _history[0];
            _currentHistoryMemoryBytes -= oldest.Pixels.Length;
            oldest.Dispose();
            _history.RemoveAt(0);
        }

        var pixels = PixelPool.Rent(bytesRequired);
        _rasterSurface.CopyPixels(pixels, stride, 0);

        var snapshot = new RasterSnapshot(_surfacePixelWidth, _surfacePixelHeight, _surfaceDpiX, _surfaceDpiY, pixels);
        _history.Add(snapshot);
        _currentHistoryMemoryBytes += pixels.Length;

        if (trackVectorSnapshot)
        {
            var strokeSnapshot = CloneInkStrokes(_inkStrokes);
            var snapshotHash = GetOrComputeInkStateHash();
            var sourcePath = _currentDocumentPath ?? string.Empty;
            var pageIndex = _currentPageIndex;
            _inkHistory.Add(new InkSnapshot(sourcePath, pageIndex, snapshotHash, strokeSnapshot));
            if (_inkHistory.Count > HistoryLimit)
            {
                _inkHistory.RemoveAt(0);
            }

            if (_photoModeActive && _currentCacheScope == InkCacheScope.Photo && !string.IsNullOrWhiteSpace(_currentDocumentPath))
            {
                _globalInkHistory.Add(new GlobalInkSnapshot(
                    _currentDocumentPath,
                    _currentPageIndex,
                    _currentCacheKey,
                    CloneInkStrokes(strokeSnapshot)));
                if (_globalInkHistory.Count > HistoryLimit)
                {
                    _globalInkHistory.RemoveAt(0);
                }
            }
        }
    }

    private bool HasDuplicateVectorSnapshot()
    {
        if (_inkHistory.Count == 0)
        {
            return false;
        }

        var last = _inkHistory[^1];
        var sourcePath = _currentDocumentPath ?? string.Empty;
        if (!string.Equals(last.SourcePath, sourcePath, StringComparison.OrdinalIgnoreCase)
            || last.PageIndex != _currentPageIndex)
        {
            return false;
        }

        // 文档/照片页：脏页跟踪器哈希即当前内容指纹（每条变更路径收尾都会刷新）。
        if (TryGetRuntimeInkHash(out var runtimeHash))
        {
            return string.Equals(last.Hash, runtimeHash, StringComparison.Ordinal);
        }

        // 白板页：跟踪器不维护，保持原有现算比对路径。
        return string.Equals(last.Hash, ComputeInkHash(_inkStrokes), StringComparison.Ordinal);
    }

    private string GetOrComputeInkStateHash()
    {
        if (TryGetRuntimeInkHash(out var runtimeHash))
        {
            return runtimeHash;
        }

        return ComputeInkHash(_inkStrokes);
    }

    private bool TryGetRuntimeInkHash(out string runtimeHash)
    {
        // 与 MarkCurrentInkPageModified 的守卫保持镜像：跟踪器只在这类页面上维护，
        // 其余情况（白板无文档等）一律现算，避免读到从未刷新的陈旧哈希。
        runtimeHash = string.Empty;
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return false;
        }

        return _inkDirtyPages.TryGetRuntimeState(
            _currentDocumentPath,
            _currentPageIndex,
            out _,
            out runtimeHash,
            out _);
    }

    private void RestoreSnapshot(RasterSnapshot snapshot)
    {
        if (_rasterSurface == null
            || snapshot.PixelWidth != _surfacePixelWidth
            || snapshot.PixelHeight != _surfacePixelHeight)
        {
            _rasterSurface = new WriteableBitmap(
                snapshot.PixelWidth,
                snapshot.PixelHeight,
                snapshot.DpiX,
                snapshot.DpiY,
                PixelFormats.Pbgra32,
                null);
            _surfacePixelWidth = snapshot.PixelWidth;
            _surfacePixelHeight = snapshot.PixelHeight;
            _surfaceDpiX = snapshot.DpiX;
            _surfaceDpiY = snapshot.DpiY;
            RasterImage.Source = _rasterSurface;
        }
        var rect = new Int32Rect(0, 0, snapshot.PixelWidth, snapshot.PixelHeight);
        var stride = snapshot.PixelWidth * 4;
        _rasterSurface.WritePixels(rect, snapshot.Pixels, stride, 0);
        _hasDrawing = true;
    }

    private void ClearInkSurfaceState()
    {
        _activeRenderer?.Reset();
        _visualHost.Clear();
        CancelPendingBrushPreview();
        _strokeInProgress = false;
        _isErasing = false;
        _lastEraserPoint = null;
        _lastCalligraphyPreviewPoint = null;
        _lastBrushInputSample = null;
        _lastBrushPredictionSample = null;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        _lastBrushAccelerationDipPerSecSq = new Vector(0, 0);
        _inkStrokes.Clear();
        ResetInkHistory();
        ClearSurface();
        _hasDrawing = false;
        RedrawInkSurface();
        MarkCurrentInkPageLoaded(_inkStrokes);
    }

    private void ClearInkSurfaceForPresentationExit()
    {
        _activeRenderer?.Reset();
        _visualHost.Clear();
        CancelPendingBrushPreview();
        _strokeInProgress = false;
        _isErasing = false;
        _lastEraserPoint = null;
        _lastCalligraphyPreviewPoint = null;
        _lastBrushInputSample = null;
        _lastBrushPredictionSample = null;
        _lastBrushVelocityDipPerSec = new Vector(0, 0);
        _lastBrushAccelerationDipPerSecSq = new Vector(0, 0);
        _inkStrokes.Clear();
        _hasDrawing = false;
        ResetInkHistory();
        ClearSurface();
        MarkCurrentInkPageLoaded(_inkStrokes);
    }

    private void SaveAndClearInkSurface()
    {
        if (!SaveCurrentPageOnNavigate(forceBackground: false))
        {
            return;
        }

        ClearInkSurfaceState();
    }
}
