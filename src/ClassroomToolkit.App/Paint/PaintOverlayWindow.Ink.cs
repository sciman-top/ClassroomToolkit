using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.Interop;
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
    private void BeginBrushStroke(WpfPoint position)
    {
        EnsureActiveRenderer();
        if (_activeRenderer == null)
        {
            return;
        }
        PushHistory();
        CaptureStrokeContext();
        _strokeInProgress = true;
        var color = EffectiveBrushColor();
        _activeRenderer.Initialize(color, _brushSize, color.A);
        _activeRenderer.OnDown(position);
        _visualHost.UpdateVisual(_activeRenderer.Render);
        _lastCalligraphyPreviewUtc = DateTime.UtcNow;
        _lastCalligraphyPreviewPoint = position;
    }

    private void UpdateBrushStroke(WpfPoint position)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        _activeRenderer.OnMove(position);
        if (_brushStyle == PaintBrushStyle.Calligraphy && ShouldThrottleCalligraphyPreview(position))
        {
            return;
        }
        _visualHost.UpdateVisual(_activeRenderer.Render);
    }

    private void EndBrushStroke(WpfPoint position)
    {
        if (!_strokeInProgress || _activeRenderer == null)
        {
            return;
        }
        _activeRenderer.OnUp(position);
        var geometry = _activeRenderer.GetLastStrokeGeometry();
        if (geometry != null)
        {
            CommitGeometryFill(geometry, EffectiveBrushColor());
            RecordBrushStroke(geometry);
        }
        _activeRenderer.Reset();
        _visualHost.Clear();
        _strokeInProgress = false;
        _lastCalligraphyPreviewPoint = null;
        if (PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                _photoModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform))
        {
            RequestInkRedraw();
        }
        SetInkContextDirty();
    }

    private bool ShouldThrottleCalligraphyPreview(WpfPoint position)
    {
        if (_lastCalligraphyPreviewPoint.HasValue)
        {
            var delta = position - _lastCalligraphyPreviewPoint.Value;
            if (delta.Length >= CalligraphyPreviewMinDistance)
            {
                _lastCalligraphyPreviewPoint = position;
                _lastCalligraphyPreviewUtc = DateTime.UtcNow;
                return false;
            }
        }
        var nowUtc = DateTime.UtcNow;
        if ((nowUtc - _lastCalligraphyPreviewUtc).TotalMilliseconds >= CalligraphyPreviewMinIntervalMs)
        {
            _lastCalligraphyPreviewUtc = nowUtc;
            _lastCalligraphyPreviewPoint = position;
            return false;
        }
        return true;
    }

    private void BeginEraser(WpfPoint position)
    {
        PushHistory();
        _isErasing = true;
        _lastEraserPoint = position;
        ApplyEraserAt(position);
    }

    private void UpdateEraser(WpfPoint position)
    {
        if (!_isErasing || _lastEraserPoint == null)
        {
            return;
        }
        var last = _lastEraserPoint.Value;
        var distance = (position - last).Length;
        var threshold = Math.Max(1.0, _eraserSize * 0.2);
        if (distance < threshold)
        {
            return;
        }
        var geometry = BuildEraserGeometry(last, position);
        if (geometry != null)
        {
            EraseGeometry(geometry);
        }
        _lastEraserPoint = position;
    }

    private void EndEraser(WpfPoint position)
    {
        if (!_isErasing)
        {
            return;
        }
        if (_lastEraserPoint == null || (_lastEraserPoint.Value - position).Length < 0.5)
        {
            ApplyEraserAt(position);
        }
        _isErasing = false;
        _lastEraserPoint = null;
        NotifyInkStateChanged(updateActiveSnapshot: true);
        if (PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                _photoModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform))
        {
            RequestInkRedraw();
        }
    }

    private void BeginRegionSelection(WpfPoint position)
    {
        PushHistory();
        _regionStart = position;
        if (_regionRect == null)
        {
            _regionRect = new WpfRectangle
            {
                Stroke = new SolidColorBrush(MediaColor.FromArgb(200, 255, 200, 60)),
                StrokeThickness = 2,
                StrokeDashArray = new DoubleCollection { 6, 4 },
                Fill = new SolidColorBrush(MediaColor.FromArgb(30, 255, 200, 60)),
                IsHitTestVisible = false
            };
            PreviewCanvas.Children.Add(_regionRect);
        }
        UpdateSelectionRect(_regionRect, _regionStart, position);
        _isRegionSelecting = true;
    }

    private void UpdateRegionSelection(WpfPoint position)
    {
        if (_isRegionSelecting && _regionRect != null)
        {
            UpdateSelectionRect(_regionRect, _regionStart, position);
        }
    }

    private void EndRegionSelection(WpfPoint position)
    {
        if (!_isRegionSelecting)
        {
            return;
        }
        _isRegionSelecting = false;
        var region = BuildRegionRect(_regionStart, position);
        ClearRegionSelection();
        if (region.Width > 2 && region.Height > 2)
        {
            EraseRect(region);
        }
        NotifyInkStateChanged(updateActiveSnapshot: true);
    }

    private void BeginShape(WpfPoint position)
    {
        if (_shapeType == PaintShapeType.None)
        {
            return;
        }
        PushHistory();
        CaptureStrokeContext();
        _shapeStart = position;
        _activeShape = CreateShape(_shapeType);
        if (_activeShape == null)
        {
            return;
        }
        ApplyShapeStyle(_activeShape);
        PreviewCanvas.Children.Add(_activeShape);
        UpdateShape(_activeShape, _shapeStart, position);
        _isDrawingShape = true;
    }

    private void UpdateShapePreview(WpfPoint position)
    {
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        UpdateShape(_activeShape, _shapeStart, position);
    }

    private void EndShape(WpfPoint position)
    {
        if (!_isDrawingShape || _activeShape == null)
        {
            return;
        }
        var geometry = BuildShapeGeometry(_shapeType, _shapeStart, position);
        if (geometry != null)
        {
            var pen = BuildShapePen();
            CommitGeometryStroke(geometry, pen);
            RecordShapeStroke(geometry, pen);
        }
        ClearShapePreview();
        if (PhotoInkRenderPolicy.ShouldRequestImmediateRedraw(
                _photoModeActive,
                RasterImage.RenderTransform,
                _photoContentTransform))
        {
            RequestInkRedraw();
        }
        NotifyInkStateChanged(updateActiveSnapshot: false);
    }
    
    private void ClearShapePreview()
    {
        if (_activeShape != null)
        {
            PreviewCanvas.Children.Remove(_activeShape);
            _activeShape = null;
        }
        _isDrawingShape = false;
    }

    private void HideEraserPreview()
    {
        // Eraser live preview is currently disabled.
    }
    
     private void ApplyEraserAt(WpfPoint position)
    {
        var radius = Math.Max(2.0, _eraserSize * 0.5);
        var geometry = new EllipseGeometry(position, radius, radius);
        EraseGeometry(geometry);
    }

    private void EraseRect(Rect region)
    {
        if (_photoModeActive)
        {
            EraseGeometry(new RectangleGeometry(region));
            return;
        }
        var eraseGeometry = new RectangleGeometry(region);
        ApplyInkErase(eraseGeometry);
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        var dpi = VisualTreeHelper.GetDpi(this);
        var rect = new Int32Rect(
            (int)Math.Floor(region.X * dpi.DpiScaleX),
            (int)Math.Floor(region.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(region.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(region.Height * dpi.DpiScaleY));
        rect = IntersectRects(rect, new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight));
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }
        var stride = rect.Width * 4;
        var clear = new byte[stride * rect.Height];
        _rasterSurface.WritePixels(rect, clear, stride, 0);
        _hasDrawing = true;
    }

    private void ClearRegionSelection()
    {
        if (_regionRect != null)
        {
            PreviewCanvas.Children.Remove(_regionRect);
            _regionRect = null;
        }
        _isRegionSelecting = false;
    }

    private void RecordBrushStroke(Geometry geometry)
    {
        if (!_inkRecordEnabled || geometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = _brushStyle,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            MaskSeed = _inkSeedRandom.Next(),
            CalligraphyInkBloomEnabled = _calligraphyInkBloomEnabled,
            CalligraphySealEnabled = _calligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = _calligraphyOverlayOpacityThreshold
        };
        if (TryGetCurrentPhotoReferenceSize(out var refWidth, out var refHeight))
        {
            stroke.ReferenceWidth = refWidth;
            stroke.ReferenceHeight = refHeight;
        }
        if (_brushStyle == PaintBrushStyle.Calligraphy && _activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
        {
            var core = calligraphyRenderer.GetLastCoreGeometry();
            var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
            var strokeGeometry = core ?? geometry;
            if (ribbons != null && ribbons.Count > 0)
            {
                var union = UnionGeometries(ribbons.Select(item => item.Geometry).ToList());
                if (union != null)
                {
                    strokeGeometry = union;
                }
            }
            var storeGeometry = _photoModeActive ? ToPhotoGeometry(strokeGeometry) : strokeGeometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
            stroke.InkFlow = calligraphyRenderer.LastInkFlow;
            stroke.StrokeDirectionX = calligraphyRenderer.LastStrokeDirection.X;
            stroke.StrokeDirectionY = calligraphyRenderer.LastStrokeDirection.Y;
            var blooms = calligraphyRenderer.GetInkBloomGeometries();
            if (blooms != null)
            {
                foreach (var bloom in blooms)
                {
                    var bloomGeometry = _photoModeActive ? ToPhotoGeometry(bloom.Geometry) : bloom.Geometry;
                    if (bloomGeometry == null)
                    {
                        continue;
                    }
                    stroke.Blooms.Add(new InkBloomData
                    {
                        GeometryPath = InkGeometrySerializer.Serialize(bloomGeometry),
                        Opacity = bloom.Opacity
                    });
                }
            }
        }
        else
        {
            var storeGeometry = _photoModeActive ? ToPhotoGeometry(geometry) : geometry;
            if (storeGeometry == null)
            {
                return;
            }
            stroke.GeometryPath = InkGeometrySerializer.Serialize(storeGeometry);
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        CommitStroke(stroke);
    }

    private void RecordShapeStroke(Geometry geometry, MediaPen pen)
    {
        if (!_inkRecordEnabled || geometry == null || pen == null)
        {
            return;
        }
        var widened = geometry.GetWidenedPathGeometry(pen);
        if (widened == null || widened.Bounds.IsEmpty)
        {
            return;
        }
        var storeGeometry = _photoModeActive ? ToPhotoGeometry(widened) : widened;
        if (storeGeometry == null)
        {
            return;
        }
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Shape,
            BrushStyle = PaintBrushStyle.StandardRibbon,
            ColorHex = ToHex(EffectiveBrushColor()),
            Opacity = _brushOpacity,
            BrushSize = _brushSize,
            MaskSeed = _inkSeedRandom.Next(),
            GeometryPath = InkGeometrySerializer.Serialize(storeGeometry)
        };
        if (TryGetCurrentPhotoReferenceSize(out var refWidth, out var refHeight))
        {
            stroke.ReferenceWidth = refWidth;
            stroke.ReferenceHeight = refHeight;
        }
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        CommitStroke(stroke);
    }

    private bool ApplyInkErase(Geometry geometry)
    {
        if (_inkStrokes.Count == 0 || geometry == null)
        {
            return false;
        }
        var erasePrimary = _photoModeActive ? ToPhotoGeometry(geometry) : geometry;
        var eraseFallback = _photoModeActive ? geometry : null;
        if (erasePrimary == null)
        {
            return false;
        }
        bool changed = false;
        for (int i = _inkStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _inkStrokes[i];
            var updatedPath = ExcludeGeometryWithFallback(stroke.GeometryPath, erasePrimary, eraseFallback);
            if (!InkStrokeEraseUpdater.TryApplyUpdatedGeometryPath(stroke, updatedPath, out var strokeRemoved))
            {
                continue;
            }
            if (strokeRemoved)
            {
                _inkStrokes.RemoveAt(i);
                changed = true;
                continue;
            }

            if (stroke.Blooms.Count > 0)
            {
                for (int j = stroke.Blooms.Count - 1; j >= 0; j--)
                {
                    var bloom = stroke.Blooms[j];
                    var bloomUpdated = ExcludeGeometryWithFallback(bloom.GeometryPath, erasePrimary, eraseFallback);
                    if (string.IsNullOrWhiteSpace(bloomUpdated))
                    {
                        stroke.Blooms.RemoveAt(j);
                        changed = true;
                        continue;
                    }
                    bloom.GeometryPath = bloomUpdated;
                }
            }
            changed = true;
        }
        return changed;
    }

    private static string? ExcludeGeometryWithFallback(string geometryPath, Geometry primaryEraser, Geometry? fallbackEraser)
    {
        var primary = ExcludeGeometry(geometryPath, primaryEraser);
        if (fallbackEraser == null)
        {
            return primary;
        }
        if (primary == null || string.Equals(primary, geometryPath, StringComparison.Ordinal))
        {
            return ExcludeGeometry(geometryPath, fallbackEraser);
        }
        return primary;
    }

    private void CaptureStrokeContext()
    {
    }

    private void CommitStroke(InkStrokeData stroke)
    {
        _inkStrokes.Add(stroke);
        NotifyInkStateChanged(updateActiveSnapshot: true, notifyContext: false);
    }

    private void UpdateActiveCacheSnapshot()
    {
        if (!_inkCacheEnabled)
        {
            return;
        }
        if (_currentCacheScope != InkCacheScope.Photo)
        {
            return;
        }
        var cacheKey = _currentCacheKey;
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }
        var strokes = CloneCommittedInkStrokes();
        if (strokes.Count == 0)
        {
            _photoCache.Remove(cacheKey);
            InvalidateNeighborInkCache(cacheKey);
            return;
        }
        _photoCache.Set(cacheKey, strokes);
        InvalidateNeighborInkCache(cacheKey);
    }

    private List<InkStrokeData> CloneCommittedInkStrokes()
    {
        return CloneInkStrokes(_inkStrokes);
    }

    private void SetInkContextDirty()
    {
        _pendingInkContextCheck = true;
        _refreshOrchestrator?.RequestRefresh("ink-dirty");
    }

    private void SetInkCacheDirty()
    {
        MarkCurrentInkPageModified();
        ScheduleSidecarAutoSave();
    }

    private bool IsCurrentPageDirty()
    {
        return _inkDirtyPages.IsDirty(_currentDocumentPath, _currentPageIndex);
    }

    private bool IsInkOperationActive()
    {
        return _strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting;
    }

    private void NotifyInkStateChanged(bool updateActiveSnapshot, bool notifyContext = true)
    {
        if (updateActiveSnapshot)
        {
            UpdateActiveCacheSnapshot();
        }
        SetInkCacheDirty();
        if (notifyContext)
        {
            SetInkContextDirty();
        }
    }

    private void MarkCurrentInkPageLoaded(IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return;
        }
        MarkInkPageLoaded(_currentDocumentPath, _currentPageIndex, strokes);
    }

    private void MarkInkPageLoaded(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }
        _inkDirtyPages.MarkLoaded(sourcePath, pageIndex, ComputeInkHash(strokes));
        ClearInkWalSnapshot(sourcePath, pageIndex);
    }

    private void MarkCurrentInkPageModified()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath) || _currentPageIndex <= 0)
        {
            return;
        }
        var hash = ComputeInkHash(_inkStrokes);
        _inkDirtyPages.MarkModified(_currentDocumentPath, _currentPageIndex, hash);
        TrackInkWalSnapshot(_currentDocumentPath, _currentPageIndex, _inkStrokes, hash);
    }

    private void MarkInkPagePersisted(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || pageIndex <= 0)
        {
            return;
        }
        _inkDirtyPages.MarkPersistedIfUnchanged(sourcePath, pageIndex, ComputeInkHash(strokes));
        ClearInkWalSnapshot(sourcePath, pageIndex);
    }

    private void TrackInkWalSnapshot(string sourcePath, int pageIndex, IReadOnlyList<InkStrokeData> strokes, string hash)
    {
        try
        {
            _inkWal.Upsert(sourcePath, pageIndex, CloneInkStrokes(strokes), hash);
        }
        catch
        {
            // Ignore WAL write failures; main flow should continue.
        }
    }

    private void ClearInkWalSnapshot(string sourcePath, int pageIndex)
    {
        try
        {
            _inkWal.Remove(sourcePath, pageIndex);
        }
        catch
        {
            // Ignore WAL cleanup failures.
        }
    }

    private void RecoverInkWalForDirectory(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || _inkPersistence == null)
        {
            return;
        }

        try
        {
            var directoryPath = System.IO.Path.GetDirectoryName(sourcePath);
            if (string.IsNullOrWhiteSpace(directoryPath))
            {
                return;
            }
            var recovered = _inkWal.RecoverDirectory(directoryPath, _inkPersistence, ComputeInkHash);
            if (recovered > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[InkWAL] Recovered {recovered} pending pages in {directoryPath}");
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[InkWAL] Recover failed: {ex.Message}");
        }
    }

    private bool WasPageModifiedInSession(string sourcePath, int pageIndex)
    {
        return _inkDirtyPages.WasModifiedInSession(sourcePath, pageIndex);
    }

    private IEnumerable<string> EnumerateSessionModifiedSourcesInDirectory(string directoryPath)
    {
        return _inkDirtyPages.EnumerateSessionModifiedSourcesInDirectory(directoryPath);
    }

    private static string ComputeInkHash(IReadOnlyList<InkStrokeData> strokes)
    {
        if (strokes == null || strokes.Count == 0)
        {
            return "empty";
        }

        var builder = new StringBuilder(strokes.Count * 64);
        foreach (var stroke in strokes)
        {
            builder.Append(stroke.Type).Append('|')
                .Append(stroke.BrushStyle).Append('|')
                .Append(stroke.ColorHex).Append('|')
                .Append(stroke.Opacity).Append('|')
                .Append(stroke.BrushSize.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.ReferenceWidth.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.ReferenceHeight.ToString("G17", CultureInfo.InvariantCulture)).Append('|')
                .Append(stroke.GeometryPath ?? string.Empty).Append('\n');
        }

        var bytes = Encoding.UTF8.GetBytes(builder.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }

    private bool TryGetCurrentPhotoReferenceSize(out double width, out double height)
    {
        width = 0;
        height = 0;
        if (!_photoModeActive || PhotoBackground.Source is not BitmapSource bitmap)
        {
            return false;
        }

        width = GetBitmapDisplayWidthInDip(bitmap);
        height = GetBitmapDisplayHeightInDip(bitmap);
        return width > 0.5 && height > 0.5;
    }

    private void MarkInkInput()
    {
        _lastInkInputUtc = DateTime.UtcNow;
        _inkDiagnostics?.OnInkInput();
        UpdateInkMonitorInterval();
    }

    private bool ShouldDeferInkContext()
    {
        if (_strokeInProgress || _isErasing || _isDrawingShape || _isRegionSelecting)
        {
            return true;
        }
        if (_lastInkInputUtc == DateTime.MinValue)
        {
            return false;
        }
        return (DateTime.UtcNow - _lastInkInputUtc).TotalMilliseconds < InkInputCooldownMs;
    }

    private void UpdateInkMonitorInterval()
    {
        var nowUtc = DateTime.UtcNow;
        var idle = _lastInkInputUtc != DateTime.MinValue
                   && (nowUtc - _lastInkInputUtc).TotalMilliseconds > InkIdleThresholdMs;

        var targetMs = idle ? InkMonitorIdleIntervalMs : InkMonitorActiveIntervalMs;
        var currentMs = _inkMonitor.Interval.TotalMilliseconds;
        if (Math.Abs(currentMs - targetMs) < 1)
        {
            return;
        }
        _inkMonitor.Interval = TimeSpan.FromMilliseconds(targetMs);
    }
}
