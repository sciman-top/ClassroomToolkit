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
    private bool _calligraphyInkBloomEnabled = true;
    private bool _calligraphySealEnabled = true;
    private byte _calligraphyOverlayOpacityThreshold = 230;
    private CalligraphyBrushPreset _calligraphyPreset = CalligraphyBrushPreset.Sharp;
    private WhiteboardBrushPreset _whiteboardPreset = WhiteboardBrushPreset.Balanced;
    private int _inkRedrawToken;
    private DateTime _lastInkRedrawUtc = DateTime.MinValue;


    // Ink Fields
    private PaintBrushStyle _brushStyle = PaintBrushStyle.Standard;
    private IBrushRenderer? _activeRenderer;
    private DrawingVisualHost _visualHost;
    private WriteableBitmap? _rasterSurface;
    private int _surfacePixelWidth;
    private int _surfacePixelHeight;
    private double _surfaceDpiX = 96.0;
    private double _surfaceDpiY = 96.0;
    private MediaColor _brushColor = Colors.Red;
    private double _brushSize = 12.0;
    private byte _brushOpacity = 255;
    private double _eraserSize = 24.0;
    private bool _isErasing;
    private bool _strokeInProgress;
    private WpfPoint? _lastEraserPoint;
    private bool _hasDrawing;
    private readonly Random _inkRandom = new Random();
    private DateTime _lastCalligraphyPreviewUtc = DateTime.MinValue;
    private WpfPoint? _lastCalligraphyPreviewPoint;
    
    // Ink History & Cache
    private readonly List<InkStrokeData> _inkStrokes = new();
    private bool _inkCacheDirty;
    private readonly List<InkSnapshot> _inkHistory = new();
    private readonly DispatcherTimer _inkMonitor;
    private readonly Random _inkSeedRandom = new Random();
    private bool _inkRecordEnabled = true;
    private bool _inkReplayPreviousEnabled;
    private int _inkRetentionDays = 30;
    private string _inkPhotoRootPath = string.Empty;
    private bool _inkCacheEnabled = true;
    private DateTime _lastInkInputUtc = DateTime.MinValue;
    private bool _pendingInkContextCheck;
    
    // Pools & Buffers
    private static readonly ArrayPool<byte> PixelPool = ArrayPool<byte>.Shared;
    private const int HistoryLimit = 20;
    private const long MaxHistoryMemoryBytes = 512 * 1024 * 1024; // 512MB
    private long _currentHistoryMemoryBytes;
    private const int InkNoiseTileCacheLimit = 96;
    private static readonly object InkNoiseTileCacheLock = new();
    private static readonly Dictionary<InkNoiseTileKey, InkNoiseTileEntry> InkNoiseTileCache = new();
    private static readonly LinkedList<InkNoiseTileKey> InkNoiseTileOrder = new();
    private byte[]? _clearSurfaceBuffer;
    private int _clearSurfaceBufferSize;

    private InkCacheScope _currentCacheScope = InkCacheScope.None;
    private InkStorageService _inkStorage = new();
    
    private readonly InkStrokeRenderer _inkStrokeRenderer = new();
    private bool _redrawPending;
    private bool _redrawInProgress;
    private bool _boardSuspendedPhotoCache;
    // Note: _photoCache and InkFinalCache seem to belong to Photo logic, but interact with Ink. 
    // Let's place InkCacheScope here.
    private enum InkCacheScope
    {
        None = 0,
        Photo = 1
    }
    
    // _photoCache usage needs to be checked. For now adding here to satisfy compiler if it's used in Ink methods.
    // If InkFinalCache is a class inside PaintOverlayWindow (it probably is), we need to move it too.
    private readonly InkFinalCache _photoCache = new(80);


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

    private sealed record InkSnapshot(List<InkStrokeData> Strokes);

    private class DrawingVisualHost : FrameworkElement
    {
        private readonly VisualCollection _children;

        public DrawingVisualHost()
        {
            _children = new VisualCollection(this);
        }

        public void AddVisual(Visual visual)
        {
            _children.Add(visual);
        }
        
        public void RemoveVisual(Visual visual)
        {
            _children.Remove(visual);
        }

        public void Clear()
        {
            _children.Clear();
        }

        public void UpdateVisual(Action<DrawingContext> renderAction)
        {
            if (_children.Count == 0)
            {
                _children.Add(new DrawingVisual());
            }

            var visual = (DrawingVisual)_children[0];
            using (var dc = visual.RenderOpen())
            {
                renderAction(dc);
            }
        }

        protected override int VisualChildrenCount => _children.Count;

        protected override Visual GetVisualChild(int index)
        {
            if (index < 0 || index >= _children.Count)
            {
                throw new ArgumentOutOfRangeException();
            }
            return _children[index];
        }
    }

    // Constants
    private const double CalligraphySealStrokeWidthFactor = 0.08;
    private const int CalligraphyPreviewMinIntervalMs = 16;
    private const double CalligraphyPreviewMinDistance = 2.0;
    private const int InkInputCooldownMs = 120;
    private const int InkMonitorActiveIntervalMs = 600;
    private const int InkMonitorIdleIntervalMs = 1400;
    private const int InkIdleThresholdMs = 2500;
    private const int InkRedrawMinIntervalMs = 16;

    // Ink Methods
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
        UpdateActiveCacheSnapshot();
        SetInkContextDirty();
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
        UpdateActiveCacheSnapshot();
        SetInkContextDirty();
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
        SetInkContextDirty();
    }


    private void ClearInkSurfaceState()
    {
        _inkStrokes.Clear();
        ResetInkHistory();
        RedrawInkSurface();
        _inkCacheDirty = false;
    }

    private void ClearInkSurfaceForPresentationExit()
    {
        _activeRenderer?.Reset();
        _visualHost.Clear();
        _strokeInProgress = false;
        _isErasing = false;
        _lastEraserPoint = null;
        _lastCalligraphyPreviewPoint = null;
        _inkStrokes.Clear();
        _hasDrawing = false;
        ResetInkHistory();
        ClearSurface();
        _inkCacheDirty = false;
    }

    private void SaveAndClearInkSurface()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
        ClearInkSurfaceState();
    }

    private void RenderStoredStroke(InkStrokeData stroke)
    {
        var geometry = stroke.CachedGeometry;
        if (geometry == null)
        {
            geometry = InkGeometrySerializer.Deserialize(stroke.GeometryPath);
            if (geometry != null)
            {
                if (geometry.CanFreeze)
                {
                    geometry.Freeze();
                }
                stroke.CachedGeometry = geometry;
                stroke.CachedBounds = geometry.Bounds;
            }
        }

        if (geometry == null)
        {
            return;
        }

        // Culling: Check if stroke is visible
        // Note: For simplicity, we only cull if not in photo mode (matrix transform is complex)
        // Or we can transform bounds.
        // Let's implement simple culling for non-photo mode first.
        if (!_photoModeActive && stroke.CachedBounds.HasValue)
        {
             var bounds = stroke.CachedBounds.Value;
             if (bounds.Right < 0 || bounds.Bottom < 0 || bounds.Left > _surfacePixelWidth || bounds.Top > _surfacePixelHeight)
             {
                 return;
             }
        }
        
        var renderGeometry = _photoModeActive ? ToScreenGeometry(geometry) : geometry;
        if (renderGeometry == null)
        {
            return;
        }
        
        // Culling for Photo Mode: check if transformed geometry is visible
        if (_photoModeActive && !renderGeometry.Bounds.IntersectsWith(new Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight)))
        {
            return;
        }
        if (!TryParseStrokeColor(stroke.ColorHex, out var color))
        {
            color = Colors.Red;
        }
        color.A = stroke.Opacity;
        if (stroke.Type == InkStrokeType.Shape || stroke.BrushStyle != PaintBrushStyle.Calligraphy)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            RenderAndBlend(renderGeometry, brush, null, erase: false, null);
            return;
        }
        var inkFlow = stroke.InkFlow;
        var strokeDirection = new Vector(stroke.StrokeDirectionX, stroke.StrokeDirectionY);
        bool suppressOverlays = stroke.Opacity < stroke.CalligraphyOverlayOpacityThreshold;
        List<(Geometry Geometry, double Opacity)>? blooms = null;
        if (stroke.CalligraphyInkBloomEnabled && stroke.Blooms.Count > 0 && !suppressOverlays)
        {
            blooms = new List<(Geometry Geometry, double Opacity)>();
            foreach (var bloom in stroke.Blooms)
            {
                var bloomGeometry = InkGeometrySerializer.Deserialize(bloom.GeometryPath);
                if (bloomGeometry == null)
                {
                    continue;
                }
                var renderBloom = _photoModeActive ? ToScreenGeometry(bloomGeometry) : bloomGeometry;
                if (renderBloom == null)
                {
                    continue;
                }
                blooms.Add((renderBloom, bloom.Opacity));
            }
        }
        RenderCalligraphyComposite(
            renderGeometry,
            color,
            stroke.BrushSize,
            inkFlow,
            strokeDirection,
            stroke.CalligraphySealEnabled,
            stroke.CalligraphyInkBloomEnabled,
            blooms,
            suppressOverlays,
            stroke.MaskSeed);
    }

    private static string? ExcludeGeometry(string geometryPath, Geometry eraser)
    {
        if (string.IsNullOrWhiteSpace(geometryPath))
        {
            return null;
        }
        var geometry = InkGeometrySerializer.Deserialize(geometryPath);
        if (geometry == null)
        {
            return null;
        }
        if (!geometry.Bounds.IntersectsWith(eraser.Bounds))
        {
            return geometryPath;
        }
        var combined = Geometry.Combine(geometry, eraser, GeometryCombineMode.Exclude, null);
        if (combined == null || combined.Bounds.IsEmpty)
        {
            return string.Empty;
        }
        return InkGeometrySerializer.Serialize(combined);
    }

    private static List<InkStrokeData> CloneInkStrokes(IEnumerable<InkStrokeData> source)
    {
        return source.Select(stroke => new InkStrokeData
        {
            Type = stroke.Type,
            BrushStyle = stroke.BrushStyle,
            GeometryPath = stroke.GeometryPath,
            ColorHex = stroke.ColorHex,
            Opacity = stroke.Opacity,
            BrushSize = stroke.BrushSize,
            MaskSeed = stroke.MaskSeed,
            InkFlow = stroke.InkFlow,
            StrokeDirectionX = stroke.StrokeDirectionX,
            StrokeDirectionY = stroke.StrokeDirectionY,
            CalligraphyInkBloomEnabled = stroke.CalligraphyInkBloomEnabled,
            CalligraphySealEnabled = stroke.CalligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = stroke.CalligraphyOverlayOpacityThreshold,
            Blooms = stroke.Blooms.Select(bloom => new InkBloomData
            {
                GeometryPath = bloom.GeometryPath,
                Opacity = bloom.Opacity
            }).ToList()
        }).ToList();
    }

    private static string ToHex(MediaColor color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private static bool TryParseStrokeColor(string? colorHex, out MediaColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return false;
        }

        try
        {
            var parsed = System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
            if (parsed is MediaColor value)
            {
                color = value;
                return true;
            }
        }
        catch
        {
            // Ignore invalid persisted color payload.
        }

        return false;
    }

    private static void UpdateSelectionRect(WpfRectangle rect, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        System.Windows.Controls.Canvas.SetLeft(rect, left);
        System.Windows.Controls.Canvas.SetTop(rect, top);
        rect.Width = Math.Max(1, width);
        rect.Height = Math.Max(1, height);
    }

    private static Rect BuildRegionRect(WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new Rect(left, top, Math.Max(1, width), Math.Max(1, height));
    }

    private void PushHistory()
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
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

        if (_inkRecordEnabled)
        {
            _inkHistory.Add(new InkSnapshot(CloneInkStrokes(_inkStrokes)));
            if (_inkHistory.Count > HistoryLimit)
            {
                _inkHistory.RemoveAt(0);
            }
        }
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

    private void EnsureRasterSurface()
    {
        if (!IsLoaded)
        {
            return;
        }
        var ensureSw = Stopwatch.StartNew();
        var dpi = VisualTreeHelper.GetDpi(this);
        var pixelWidth = Math.Max(1, (int)Math.Round(ActualWidth * dpi.DpiScaleX));
        var pixelHeight = Math.Max(1, (int)Math.Round(ActualHeight * dpi.DpiScaleY));
        if (_rasterSurface != null
            && pixelWidth == _surfacePixelWidth
            && pixelHeight == _surfacePixelHeight)
        {
            _perfEnsureSurface.Add(ensureSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        var newSurface = new WriteableBitmap(
            pixelWidth,
            pixelHeight,
            dpi.PixelsPerInchX,
            dpi.PixelsPerInchY,
            PixelFormats.Pbgra32,
            null);
        if (_rasterSurface != null)
        {
            CopyBitmapToSurface(_rasterSurface, newSurface);
        }
        _rasterSurface = newSurface;
        _surfacePixelWidth = pixelWidth;
        _surfacePixelHeight = pixelHeight;
        _surfaceDpiX = dpi.PixelsPerInchX;
        _surfaceDpiY = dpi.PixelsPerInchY;
        RasterImage.Source = _rasterSurface;
        _perfEnsureSurface.Add(ensureSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void ClearSurface()
    {
        var clearSw = Stopwatch.StartNew();
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            _perfClearSurface.Add(clearSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        var rect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        var stride = _surfacePixelWidth * 4;
        var bytesRequired = stride * _surfacePixelHeight;
        if (_clearSurfaceBuffer == null || _clearSurfaceBufferSize < bytesRequired)
        {
            _clearSurfaceBuffer = new byte[bytesRequired];
            _clearSurfaceBufferSize = bytesRequired;
        }
        _rasterSurface.WritePixels(rect, _clearSurfaceBuffer, stride, 0);
        _perfClearSurface.Add(clearSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void CopyBitmapToSurface(BitmapSource source, WriteableBitmap target)
    {
        var stride = target.PixelWidth * 4;
        if (source.PixelWidth == target.PixelWidth && source.PixelHeight == target.PixelHeight)
        {
            var pixels = new byte[stride * target.PixelHeight];
            source.CopyPixels(pixels, stride, 0);
            target.WritePixels(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight), pixels, stride, 0);
            return;
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var dipWidth = target.PixelWidth * 96.0 / target.DpiX;
            var dipHeight = target.PixelHeight * 96.0 / target.DpiY;
            dc.DrawImage(source, new Rect(0, 0, dipWidth, dipHeight));
        }
        var rtb = new RenderTargetBitmap(target.PixelWidth, target.PixelHeight, target.DpiX, target.DpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);
        var pixelsOut = new byte[stride * target.PixelHeight];
        rtb.CopyPixels(pixelsOut, stride, 0);
        target.WritePixels(new Int32Rect(0, 0, target.PixelWidth, target.PixelHeight), pixelsOut, stride, 0);
    }

    private void CommitGeometryFill(Geometry geometry, MediaColor color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        bool isCalligraphy = _brushStyle == PaintBrushStyle.Calligraphy;
        bool suppressOverlays = ShouldSuppressCalligraphyOverlays();
        double inkFlow = 1.0;
        Vector? strokeDirection = null;
        if (isCalligraphy)
        {
            if (_activeRenderer is VariableWidthBrushRenderer calligraphyRenderer)
            {
                inkFlow = calligraphyRenderer.LastInkFlow;
                strokeDirection = calligraphyRenderer.LastStrokeDirection;
                var coreGeometry = calligraphyRenderer.GetLastCoreGeometry();
                if (coreGeometry != null)
                {
                    var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
                    var strokeGeometry = coreGeometry;
                    if (ribbons != null && ribbons.Count > 0)
                    {
                        var union = UnionGeometries(ribbons.Select(item => item.Geometry).ToList());
                        if (union != null)
                        {
                            strokeGeometry = union;
                        }
                    }
                    var blooms = _calligraphyInkBloomEnabled
                        ? calligraphyRenderer.GetInkBloomGeometries()
                        : null;
                    RenderCalligraphyComposite(
                        strokeGeometry,
                        color,
                        _brushSize,
                        inkFlow,
                        strokeDirection,
                        _calligraphySealEnabled,
                        _calligraphyInkBloomEnabled,
                        blooms?.Select(bloom => (bloom.Geometry, bloom.Opacity)),
                        suppressOverlays,
                        maskSeed: null);
                    return;
                }
            }
        }
        if (isCalligraphy)
        {
            RenderInkLayers(geometry, color, inkFlow, 1.0, strokeDirection);
            return;
        }
        RenderAndBlend(geometry, brush, null, erase: false, null);
    }

    private void CommitGeometryStroke(Geometry geometry, MediaPen pen)
    {
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void EraseGeometry(Geometry geometry)
    {
        ApplyInkErase(geometry);
        RenderAndBlend(geometry, MediaBrushes.White, null, erase: true, null);
    }

    private void RenderInkLayers(Geometry geometry, MediaColor color, double inkFlow, double ribbonOpacity, Vector? strokeDirection)
    {
        var solidBrush = new SolidColorBrush(color)
        {
            Opacity = Math.Clamp(ribbonOpacity, 0.1, 1.0)
        };
        solidBrush.Freeze();
        var mask = IsInkMaskEligible(geometry)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
            : null;
        RenderAndBlend(geometry, solidBrush, null, erase: false, mask);
    }

    private void RenderInkCore(Geometry geometry, MediaColor color, bool enableSeal)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        RenderAndBlend(geometry, brush, null, erase: false, null);
        if (!enableSeal || !_calligraphySealEnabled)
        {
            return;
        }
        double sealWidth = Math.Max(_brushSize * CalligraphySealStrokeWidthFactor, 0.6);
        if (sealWidth <= 0)
        {
            return;
        }
        var pen = new MediaPen(brush, sealWidth);
        pen.Freeze();
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private static Geometry? UnionGeometries(IReadOnlyList<Geometry> geometries)
    {
        if (geometries.Count == 0)
        {
            return null;
        }

        Geometry combined = geometries[0];
        for (int i = 1; i < geometries.Count; i++)
        {
            combined = new CombinedGeometry(GeometryCombineMode.Union, combined, geometries[i]);
        }
        combined.Freeze();
        return combined;
    }

    private void RenderInkSeal(Geometry geometry, MediaColor color)
    {
        if (!_calligraphySealEnabled)
        {
            return;
        }
        double sealWidth = Math.Max(_brushSize * CalligraphySealStrokeWidthFactor, 0.6);
        if (sealWidth <= 0)
        {
            return;
        }
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        var pen = new MediaPen(brush, sealWidth);
        pen.Freeze();
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void RenderInkEdge(Geometry coreGeometry, MediaColor color, double inkFlow, Vector? strokeDirection)
    {
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(_brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgeBrush = new SolidColorBrush(color)
        {
            Opacity = edgeOpacity
        };
        edgeBrush.Freeze();
        var pen = new MediaPen(edgeBrush, edgeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        pen.Freeze();
        var mask = IsInkMaskEligible(coreGeometry)
            ? BuildInkOpacityMask(coreGeometry.Bounds, inkFlow, strokeDirection)
            : null;
        RenderAndBlend(coreGeometry, null, pen, erase: false, mask);
    }

    private readonly struct DrawCommand
    {
        public DrawCommand(Geometry geometry, MediaBrush? fill, MediaPen? pen, MediaBrush? opacityMask, Geometry? clipGeometry)
        {
            Geometry = geometry;
            Fill = fill;
            Pen = pen;
            OpacityMask = opacityMask;
            ClipGeometry = clipGeometry;
        }

        public Geometry Geometry { get; }
        public MediaBrush? Fill { get; }
        public MediaPen? Pen { get; }
        public MediaBrush? OpacityMask { get; }
        public Geometry? ClipGeometry { get; }
    }

    private void RenderCalligraphyComposite(
        Geometry geometry,
        MediaColor color,
        double brushSize,
        double inkFlow,
        Vector? strokeDirection,
        bool sealEnabled,
        bool bloomEnabled,
        IEnumerable<(Geometry Geometry, double Opacity)>? blooms,
        bool suppressOverlays,
        int? maskSeed)
    {
        var commands = new List<DrawCommand>();

        if (!suppressOverlays && bloomEnabled && blooms != null)
        {
            foreach (var bloom in blooms)
            {
                var bloomBrush = new SolidColorBrush(color)
                {
                    Opacity = bloom.Opacity
                };
                bloomBrush.Freeze();
                commands.Add(new DrawCommand(bloom.Geometry, bloomBrush, null, null, geometry));
            }
        }

        var coreBrush = new SolidColorBrush(color);
        coreBrush.Freeze();
        commands.Add(new DrawCommand(geometry, coreBrush, null, null, null));

        if (!suppressOverlays && sealEnabled)
        {
            double sealWidth = Math.Max(brushSize * CalligraphySealStrokeWidthFactor, 0.6);
            if (sealWidth > 0)
            {
                var sealPen = new MediaPen(coreBrush, sealWidth)
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round
                };
                sealPen.Freeze();
                commands.Add(new DrawCommand(geometry, null, sealPen, null, null));
            }
        }

        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgeBrush = new SolidColorBrush(color)
        {
            Opacity = edgeOpacity
        };
        edgeBrush.Freeze();
        var edgePen = new MediaPen(edgeBrush, edgeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round
        };
        edgePen.Freeze();
        var edgeMask = maskSeed.HasValue
            ? (IsInkMaskEligible(geometry, brushSize)
                ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed.Value)
                : null)
            : (IsInkMaskEligible(geometry)
                ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
                : null);
        commands.Add(new DrawCommand(geometry, null, edgePen, edgeMask, null));

        if (!suppressOverlays)
        {
            var ribbonBrush = new SolidColorBrush(color)
            {
                Opacity = Math.Clamp(0.28, 0.1, 1.0)
            };
            ribbonBrush.Freeze();
            var ribbonMask = maskSeed.HasValue
                ? (IsInkMaskEligible(geometry, brushSize)
                    ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed.Value)
                    : null)
                : (IsInkMaskEligible(geometry)
                    ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
                    : null);
            commands.Add(new DrawCommand(geometry, ribbonBrush, null, ribbonMask, null));
        }

        RenderAndBlendBatch(commands);
    }

    private bool ShouldSuppressCalligraphyOverlays()
    {
        return _brushOpacity < _calligraphyOverlayOpacityThreshold;
    }

    private bool IsInkMaskEligible(Geometry geometry)
    {
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }
        var bounds = geometry.Bounds;
        double minSize = Math.Max(_brushSize * 1.0, 14.0);
        return bounds.Width >= minSize && bounds.Height >= minSize;
    }

    private static bool IsInkMaskEligible(Geometry geometry, double brushSize)
    {
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }
        var bounds = geometry.Bounds;
        double minSize = Math.Max(brushSize * 1.0, 14.0);
        return bounds.Width >= minSize && bounds.Height >= minSize;
    }

    private void RenderAndBlend(Geometry geometry, MediaBrush? fill, MediaPen? pen, bool erase, MediaBrush? opacityMask, Geometry? clipGeometry = null)
    {
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        if (!TryRenderGeometry(geometry, fill, pen, opacityMask, clipGeometry, out var rect, out var pixels, out var stride, out var bufferLength))
        {
            return;
        }
        try
        {
            if (erase)
            {
                ApplyEraseMask(rect, pixels, stride);
            }
            else
            {
                BlendSourceOver(rect, pixels, stride);
            }
            _hasDrawing = true;
        }
        finally
        {
            PixelPool.Return(pixels, clearArray: false);
        }
    }

    private void RenderAndBlendBatch(IReadOnlyList<DrawCommand> commands)
    {
        if (commands == null || commands.Count == 0)
        {
            return;
        }
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            return;
        }
        if (!TryRenderGeometryBatch(commands, out var rect, out var pixels, out var stride, out var bufferLength))
        {
            return;
        }
        try
        {
            BlendSourceOver(rect, pixels, stride);
            _hasDrawing = true;
        }
        finally
        {
            PixelPool.Return(pixels, clearArray: false);
        }
    }

    private bool TryRenderGeometry(
        Geometry geometry,
        MediaBrush? fill,
        MediaPen? pen,
        MediaBrush? opacityMask,
        Geometry? clipGeometry,
        out Int32Rect destRect,
        out byte[] pixels,
        out int stride,
        out int bufferLength)
    {
        destRect = new Int32Rect(0, 0, 0, 0);
        pixels = Array.Empty<byte>();
        stride = 0;
        bufferLength = 0;
        if (_rasterSurface == null || geometry == null)
        {
            return false;
        }
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }
        var bounds = pen != null ? geometry.GetRenderBounds(pen) : geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return false;
        }
        bounds.Inflate(2, 2);
        var dpi = VisualTreeHelper.GetDpi(this);
        var rawRect = new Int32Rect(
            (int)Math.Floor(bounds.X * dpi.DpiScaleX),
            (int)Math.Floor(bounds.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(bounds.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(bounds.Height * dpi.DpiScaleY));
        var surfaceRect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        destRect = IntersectRects(rawRect, surfaceRect);
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var offsetX = destRect.X / dpi.DpiScaleX;
            var offsetY = destRect.Y / dpi.DpiScaleY;
            dc.PushTransform(new TranslateTransform(-offsetX, -offsetY));
            if (clipGeometry != null)
            {
                dc.PushClip(clipGeometry);
            }
            if (opacityMask != null)
            {
                dc.PushOpacityMask(opacityMask);
            }
            dc.DrawGeometry(fill, pen, geometry);
            if (opacityMask != null)
            {
                dc.Pop();
            }
            if (clipGeometry != null)
            {
                dc.Pop();
            }
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(destRect.Width, destRect.Height, _surfaceDpiX, _surfaceDpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);
        stride = destRect.Width * 4;
        bufferLength = stride * destRect.Height;
        pixels = PixelPool.Rent(bufferLength);
        rtb.CopyPixels(pixels, stride, 0);
        return true;
    }

    private bool TryRenderGeometryBatch(
        IReadOnlyList<DrawCommand> commands,
        out Int32Rect destRect,
        out byte[] pixels,
        out int stride,
        out int bufferLength)
    {
        destRect = new Int32Rect(0, 0, 0, 0);
        pixels = Array.Empty<byte>();
        stride = 0;
        bufferLength = 0;
        if (_rasterSurface == null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var surfaceRect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        bool hasBounds = false;
        var unionRect = new Int32Rect(0, 0, 0, 0);

        foreach (var command in commands)
        {
            var geometry = command.Geometry;
            if (geometry == null || geometry.Bounds.IsEmpty)
            {
                continue;
            }
            var bounds = command.Pen != null ? geometry.GetRenderBounds(command.Pen) : geometry.Bounds;
            if (bounds.IsEmpty)
            {
                continue;
            }
            bounds.Inflate(2, 2);
            var rawRect = new Int32Rect(
                (int)Math.Floor(bounds.X * dpi.DpiScaleX),
                (int)Math.Floor(bounds.Y * dpi.DpiScaleY),
                (int)Math.Ceiling(bounds.Width * dpi.DpiScaleX),
                (int)Math.Ceiling(bounds.Height * dpi.DpiScaleY));
            if (!hasBounds)
            {
                unionRect = rawRect;
                hasBounds = true;
            }
            else
            {
                unionRect = UnionRects(unionRect, rawRect);
            }
        }

        if (!hasBounds)
        {
            return false;
        }
        destRect = IntersectRects(unionRect, surfaceRect);
        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var offsetX = destRect.X / dpi.DpiScaleX;
            var offsetY = destRect.Y / dpi.DpiScaleY;
            dc.PushTransform(new TranslateTransform(-offsetX, -offsetY));
            foreach (var command in commands)
            {
                if (command.ClipGeometry != null)
                {
                    dc.PushClip(command.ClipGeometry);
                }
                if (command.OpacityMask != null)
                {
                    dc.PushOpacityMask(command.OpacityMask);
                }
                dc.DrawGeometry(command.Fill, command.Pen, command.Geometry);
                if (command.OpacityMask != null)
                {
                    dc.Pop();
                }
                if (command.ClipGeometry != null)
                {
                    dc.Pop();
                }
            }
            dc.Pop();
        }
        var rtb = new RenderTargetBitmap(destRect.Width, destRect.Height, _surfaceDpiX, _surfaceDpiY, PixelFormats.Pbgra32);
        rtb.Render(visual);
        stride = destRect.Width * 4;
        bufferLength = stride * destRect.Height;
        pixels = PixelPool.Rent(bufferLength);
        rtb.CopyPixels(pixels, stride, 0);
        return true;
    }

    private MediaBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector? strokeDirection)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }

        int tileSize = (int)Math.Round(Math.Clamp(_brushSize * 2.2, 18, 90));
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double baseAlpha = Lerp(0.68, 0.96, inkFlow);
        double variation = Lerp(0.08, 0.24, dryFactor);
        var tile = CreateInkNoiseTile(tileSize, baseAlpha, variation, _inkRandom.Next());

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.72 + (inkFlow * 0.28), 0.6, 1.0)
        };
        ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor);
        texture.Freeze();

        var centerOpacity = Math.Clamp(0.95 + (inkFlow * 0.05), 0.85, 1.0);
        var edgeOpacity = Math.Clamp(0.72 + (inkFlow * 0.08), 0.6, 0.9);
        var radial = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new WpfPoint(bounds.X + bounds.Width * 0.5, bounds.Y + bounds.Height * 0.5),
            GradientOrigin = new WpfPoint(bounds.X + bounds.Width * 0.48, bounds.Y + bounds.Height * 0.48),
            RadiusX = bounds.Width * 0.55,
            RadiusY = bounds.Height * 0.55
        };
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(MediaBrushes.White, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(radial, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(texture, null, new RectangleGeometry(bounds)));
        group.Freeze();
        return new DrawingBrush(group) { Stretch = Stretch.None };
    }

    private static MediaBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector? strokeDirection, double brushSize, int seed)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }
        int tileSize = (int)Math.Round(Math.Clamp(brushSize * 2.2, 18, 90));
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double baseAlpha = Lerp(0.68, 0.96, inkFlow);
        double variation = Lerp(0.08, 0.24, dryFactor);
        int effectiveSeed = seed == 0 ? 17 : seed;
        var tile = CreateInkNoiseTile(tileSize, baseAlpha, variation, effectiveSeed);

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.72 + (inkFlow * 0.28), 0.6, 1.0)
        };
        ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor);
        texture.Freeze();

        var centerOpacity = Math.Clamp(0.95 + (inkFlow * 0.05), 0.85, 1.0);
        var edgeOpacity = Math.Clamp(0.72 + (inkFlow * 0.08), 0.6, 0.9);
        var radial = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new WpfPoint(bounds.X + bounds.Width * 0.5, bounds.Y + bounds.Height * 0.5),
            GradientOrigin = new WpfPoint(bounds.X + bounds.Width * 0.48, bounds.Y + bounds.Height * 0.48),
            RadiusX = bounds.Width * 0.55,
            RadiusY = bounds.Height * 0.55
        };
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(MediaBrushes.White, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(radial, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(texture, null, new RectangleGeometry(bounds)));
        group.Freeze();
        return new DrawingBrush(group) { Stretch = Stretch.None };
    }

    private static void ApplyInkTextureTransform(ImageBrush brush, Rect bounds, Vector? strokeDirection, double dryFactor)
    {
        var dir = strokeDirection ?? new Vector(1, 0);
        if (dir.LengthSquared < 0.0001)
        {
            dir = new Vector(1, 0);
        }
        else
        {
            dir.Normalize();
        }

        double angle = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI;
        double centerX = bounds.X + bounds.Width * 0.5;
        double centerY = bounds.Y + bounds.Height * 0.5;
        double stretch = Lerp(1.3, 1.8, dryFactor);
        double squash = Lerp(0.85, 0.6, dryFactor);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(stretch, squash, centerX, centerY));
        transforms.Children.Add(new RotateTransform(angle, centerX, centerY));
        brush.Transform = transforms;
    }

    private sealed class InkNoiseTileEntry
    {
        public InkNoiseTileEntry(BitmapSource tile, LinkedListNode<InkNoiseTileKey> node)
        {
            Tile = tile;
            Node = node;
        }

        public BitmapSource Tile { get; }
        public LinkedListNode<InkNoiseTileKey> Node { get; }
    }

    private readonly struct InkNoiseTileKey : IEquatable<InkNoiseTileKey>
    {
        public InkNoiseTileKey(int size, int seed, double baseAlpha, double variation)
        {
            Size = size;
            Seed = seed;
            BaseAlphaBits = BitConverter.DoubleToInt64Bits(baseAlpha);
            VariationBits = BitConverter.DoubleToInt64Bits(variation);
        }

        public int Size { get; }
        public int Seed { get; }
        public long BaseAlphaBits { get; }
        public long VariationBits { get; }

        public bool Equals(InkNoiseTileKey other)
        {
            return Size == other.Size
                && Seed == other.Seed
                && BaseAlphaBits == other.BaseAlphaBits
                && VariationBits == other.VariationBits;
        }

        public override bool Equals(object? obj)
        {
            return obj is InkNoiseTileKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Size, Seed, BaseAlphaBits, VariationBits);
        }
    }

    private static BitmapSource CreateInkNoiseTile(int size, double baseAlpha, double variation, int seed)
    {
        var key = new InkNoiseTileKey(size, seed, baseAlpha, variation);
        lock (InkNoiseTileCacheLock)
        {
            if (InkNoiseTileCache.TryGetValue(key, out var entry))
            {
                InkNoiseTileOrder.Remove(entry.Node);
                InkNoiseTileOrder.AddLast(entry.Node);
                return entry.Tile;
            }
        }

        var bitmap = CreateInkNoiseTileCore(size, baseAlpha, variation, seed);
        lock (InkNoiseTileCacheLock)
        {
            if (InkNoiseTileCache.TryGetValue(key, out var existing))
            {
                InkNoiseTileOrder.Remove(existing.Node);
                InkNoiseTileOrder.AddLast(existing.Node);
                return existing.Tile;
            }
            var node = InkNoiseTileOrder.AddLast(key);
            InkNoiseTileCache[key] = new InkNoiseTileEntry(bitmap, node);
            while (InkNoiseTileOrder.Count > InkNoiseTileCacheLimit)
            {
                var oldest = InkNoiseTileOrder.First;
                if (oldest == null)
                {
                    break;
                }
                InkNoiseTileOrder.RemoveFirst();
                InkNoiseTileCache.Remove(oldest.Value);
            }
        }
        return bitmap;
    }

    private static BitmapSource CreateInkNoiseTileCore(int size, double baseAlpha, double variation, int seed)
    {
        var rng = new Random(seed);
        int grid = 14;
        var gridValues = new double[grid + 1, grid + 1];

        for (int y = 0; y <= grid; y++)
        {
            for (int x = 0; x <= grid; x++)
            {
                double jitter = (rng.NextDouble() * 2.0 - 1.0) * variation;
                gridValues[x, y] = Math.Clamp(baseAlpha + jitter, 0.0, 1.0);
            }
        }

        double angle = rng.NextDouble() * Math.PI;
        double fx = Math.Cos(angle);
        double fy = Math.Sin(angle);
        double fiberFreq = 2.6 + rng.NextDouble() * 2.2;
        double fiberPhase = rng.NextDouble() * Math.PI * 2.0;
        double fiberAmp = variation * 0.2;

        int stride = size * 4;
        var pixels = new byte[stride * size];
        double scale = grid / (double)(size - 1);

        for (int y = 0; y < size; y++)
        {
            double gy = y * scale;
            int y0 = (int)Math.Floor(gy);
            int y1 = Math.Min(y0 + 1, grid);
            double ty = gy - y0;

            for (int x = 0; x < size; x++)
            {
                double gx = x * scale;
                int x0 = (int)Math.Floor(gx);
                int x1 = Math.Min(x0 + 1, grid);
                double tx = gx - x0;

                double n0 = Lerp(gridValues[x0, y0], gridValues[x1, y0], tx);
                double n1 = Lerp(gridValues[x0, y1], gridValues[x1, y1], tx);
                double noise = Lerp(n0, n1, ty);

                double fiber = Math.Sin(((x * fx + y * fy) / size) * (Math.PI * 2.0 * fiberFreq) + fiberPhase) * fiberAmp;
                double value = Math.Clamp(noise + fiber, 0.0, 1.0);
                byte alpha = (byte)Math.Round(value * 255);

                int idx = (y * size + x) * 4;
                pixels[idx] = alpha;
                pixels[idx + 1] = alpha;
                pixels[idx + 2] = alpha;
                pixels[idx + 3] = alpha;
            }
        }

        var bitmap = new WriteableBitmap(size, size, 96, 96, PixelFormats.Pbgra32, null);
        bitmap.WritePixels(new Int32Rect(0, 0, size, size), pixels, stride, 0);
        bitmap.Freeze();
        return bitmap;
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    private void BlendSourceOver(Int32Rect rect, byte[] srcPixels, int srcStride)
    {
        if (_rasterSurface == null)
        {
            return;
        }
        var destStride = rect.Width * 4;
        var destLength = destStride * rect.Height;
        var destPixels = PixelPool.Rent(destLength);
        _rasterSurface.CopyPixels(rect, destPixels, destStride, 0);
        for (int y = 0; y < rect.Height; y++)
        {
            var srcRow = y * srcStride;
            var destRow = y * destStride;
            for (int x = 0; x < rect.Width; x++)
            {
                int i = srcRow + x * 4;
                byte srcA = srcPixels[i + 3];
                if (srcA == 0)
                {
                    continue;
                }
                int invA = 255 - srcA;
                int d = destRow + x * 4;
                destPixels[d] = (byte)(srcPixels[i] + destPixels[d] * invA / 255);
                destPixels[d + 1] = (byte)(srcPixels[i + 1] + destPixels[d + 1] * invA / 255);
                destPixels[d + 2] = (byte)(srcPixels[i + 2] + destPixels[d + 2] * invA / 255);
                destPixels[d + 3] = (byte)(srcA + destPixels[d + 3] * invA / 255);
            }
        }
        _rasterSurface.WritePixels(rect, destPixels, destStride, 0);
        PixelPool.Return(destPixels, clearArray: false);
    }

    private void ApplyEraseMask(Int32Rect rect, byte[] maskPixels, int maskStride)
    {
        if (_rasterSurface == null)
        {
            return;
        }
        var destStride = rect.Width * 4;
        var destLength = destStride * rect.Height;
        var destPixels = PixelPool.Rent(destLength);
        _rasterSurface.CopyPixels(rect, destPixels, destStride, 0);
        for (int y = 0; y < rect.Height; y++)
        {
            var maskRow = y * maskStride;
            var destRow = y * destStride;
            for (int x = 0; x < rect.Width; x++)
            {
                int i = maskRow + x * 4;
                byte maskA = maskPixels[i + 3];
                if (maskA == 0)
                {
                    continue;
                }
                int invA = 255 - maskA;
                int d = destRow + x * 4;
                destPixels[d] = (byte)(destPixels[d] * invA / 255);
                destPixels[d + 1] = (byte)(destPixels[d + 1] * invA / 255);
                destPixels[d + 2] = (byte)(destPixels[d + 2] * invA / 255);
                destPixels[d + 3] = (byte)(destPixels[d + 3] * invA / 255);
            }
        }
        _rasterSurface.WritePixels(rect, destPixels, destStride, 0);
        PixelPool.Return(destPixels, clearArray: false);
    }

    private static Int32Rect IntersectRects(Int32Rect a, Int32Rect b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return new Int32Rect(0, 0, 0, 0);
        }
        return new Int32Rect(x, y, width, height);
    }

    private static Int32Rect UnionRects(Int32Rect a, Int32Rect b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        int right = Math.Max(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return new Int32Rect(0, 0, 0, 0);
        }
        return new Int32Rect(x, y, width, height);
    }


    private sealed class RefreshOrchestrator
    {
        private readonly Dispatcher _dispatcher;
        private readonly Action _refreshAction;
        private int _refreshRunning;
        private int _refreshRequested;

        public RefreshOrchestrator(Dispatcher dispatcher, Action refreshAction)
        {
            _dispatcher = dispatcher;
            _refreshAction = refreshAction;
        }

        public void RequestRefresh(string reason)
        {
            Interlocked.Exchange(ref _refreshRequested, 1);
            if (Interlocked.CompareExchange(ref _refreshRunning, 1, 0) == 0)
            {
                _dispatcher.BeginInvoke(new Action(Drain), DispatcherPriority.Background);
            }
        }

        private void Drain()
        {
            int loops = 0;
            while (true)
            {
                Interlocked.Exchange(ref _refreshRequested, 0);
                _refreshAction();
                loops++;
                if (Interlocked.CompareExchange(ref _refreshRequested, 0, 0) == 0 || loops >= 2)
                {
                    Interlocked.Exchange(ref _refreshRunning, 0);
                    if (Interlocked.Exchange(ref _refreshRequested, 0) == 1
                        && Interlocked.CompareExchange(ref _refreshRunning, 1, 0) == 0)
                    {
                        _dispatcher.BeginInvoke(new Action(Drain), DispatcherPriority.Background);
                    }
                    return;
                }
            }
        }
    }

    private sealed class PerfStats
    {
        private readonly string _name;
        private int _count;
        private double _total;
        private double _max;
        private int _uiThreadSamples;
        private DateTime _lastLogUtc = DateTime.MinValue;

        public PerfStats(string name)
        {
            _name = name;
        }

        public void Add(double ms, bool uiThread)
        {
            _count++;
            _total += ms;
            if (ms > _max)
            {
                _max = ms;
            }
            if (uiThread)
            {
                _uiThreadSamples++;
            }

            if (_count < 30 && (DateTime.UtcNow - _lastLogUtc).TotalSeconds < 30)
            {
                return;
            }
            if (_count % 30 != 0 && (DateTime.UtcNow - _lastLogUtc).TotalSeconds < 30)
            {
                return;
            }

            var avg = _total / Math.Max(1, _count);
            var uiRatio = _count == 0 ? 0 : (double)_uiThreadSamples / _count * 100.0;
            System.Diagnostics.Debug.WriteLine(
                $"[Perf] {_name}: avg={avg:F2}ms max={_max:F2}ms samples={_count} ui={uiRatio:F0}%");
            _lastLogUtc = DateTime.UtcNow;
        }
    }
    private Shape? CreateShape(PaintShapeType type)
    {
        return type switch
        {
            PaintShapeType.None => null,
            PaintShapeType.Line => new Line(),
            PaintShapeType.DashedLine => new Line(),
            PaintShapeType.Rectangle => new System.Windows.Shapes.Rectangle(),
            PaintShapeType.RectangleFill => new System.Windows.Shapes.Rectangle(),
            PaintShapeType.Ellipse => new Ellipse(),
            PaintShapeType.Path => new WpfPath(),
            _ => null
        };
    }

    private void ApplyShapeStyle(Shape shape)
    {
        var stroke = new SolidColorBrush(EffectiveBrushColor());
        stroke.Freeze();
        shape.Stroke = stroke;
        shape.StrokeThickness = Math.Max(1, _brushSize);
        shape.StrokeStartLineCap = PenLineCap.Round;
        shape.StrokeEndLineCap = PenLineCap.Round;
        shape.StrokeLineJoin = PenLineJoin.Round;
        if (_shapeType == PaintShapeType.DashedLine)
        {
            shape.StrokeDashArray = new DoubleCollection { 6, 4 };
        }
        shape.Fill = null;
        shape.IsHitTestVisible = false;
    }

    private static void UpdateShape(Shape shape, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (shape is Line line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
        }
        else
        {
            System.Windows.Controls.Canvas.SetLeft(shape, left);
            System.Windows.Controls.Canvas.SetTop(shape, top);
            shape.Width = Math.Max(1, width);
            shape.Height = Math.Max(1, height);
        }
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

    private Geometry? BuildShapeGeometry(PaintShapeType type, WpfPoint start, WpfPoint end)
    {
        var rect = new Rect(start, end);
        return type switch
        {
            PaintShapeType.Line => new LineGeometry(start, end),
            PaintShapeType.DashedLine => new LineGeometry(start, end),
            PaintShapeType.Rectangle => new RectangleGeometry(rect),
            PaintShapeType.RectangleFill => new RectangleGeometry(rect),
            PaintShapeType.Ellipse => new EllipseGeometry(rect),
            _ => null
        };
    }

    private MediaPen BuildShapePen()
    {
        var brush = new SolidColorBrush(EffectiveBrushColor());
        brush.Freeze();
        var pen = new MediaPen(brush, Math.Max(1.0, _brushSize))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (_shapeType == PaintShapeType.DashedLine)
        {
            pen.DashStyle = new DashStyle(new double[] { 6, 4 }, 0);
            pen.DashCap = PenLineCap.Round;
        }
        pen.Freeze();
        return pen;
    }

    private Geometry? BuildEraserGeometry(WpfPoint start, WpfPoint end)
    {
        var radius = Math.Max(2.0, _eraserSize * 0.5);
        var delta = end - start;
        if (delta.Length < 0.5)
        {
            return new EllipseGeometry(start, radius, radius);
        }
        var path = new StreamGeometry();
        using (var ctx = path.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.LineTo(end, isStroked: true, isSmoothJoin: true);
        }
        var pen = new MediaPen(MediaBrushes.Black, Math.Max(1.0, _eraserSize))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        return path.GetWidenedPathGeometry(pen);
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
        if (string.IsNullOrWhiteSpace(stroke.GeometryPath))
        {
            return;
        }
        CommitStroke(stroke);
    }

    private void ApplyInkErase(Geometry geometry)
    {
        if (!_inkRecordEnabled || _inkStrokes.Count == 0 || geometry == null)
        {
            return;
        }
        var eraseGeometry = _photoModeActive ? ToPhotoGeometry(geometry) : geometry;
        if (eraseGeometry == null)
        {
            return;
        }
        bool changed = false;
        for (int i = _inkStrokes.Count - 1; i >= 0; i--)
        {
            var stroke = _inkStrokes[i];
            var updatedPath = ExcludeGeometry(stroke.GeometryPath, eraseGeometry);
            if (updatedPath == null)
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(updatedPath))
            {
                _inkStrokes.RemoveAt(i);
                changed = true;
                continue;
            }
            stroke.GeometryPath = updatedPath;
            if (stroke.Blooms.Count > 0)
            {
                for (int j = stroke.Blooms.Count - 1; j >= 0; j--)
                {
                    var bloom = stroke.Blooms[j];
                    var bloomUpdated = ExcludeGeometry(bloom.GeometryPath, eraseGeometry);
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
        if (changed)
        {
            SetInkCacheDirty();
        }
    }

    private void CaptureStrokeContext()
    {
    }

    private void CommitStroke(InkStrokeData stroke)
    {
        _inkStrokes.Add(stroke);
        if (_currentCacheScope == InkCacheScope.Photo)
        {
            SetInkCacheDirty();
            UpdateActiveCacheSnapshot();
        }
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
            return;
        }
        _photoCache.Set(cacheKey, strokes);
    }

    private List<InkStrokeData> CloneCommittedInkStrokes()
    {
        return CloneInkStrokes(_inkStrokes);
    }

    private void RedrawInkSurface()
    {
        var redrawSw = Stopwatch.StartNew();
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        if (_inkStrokes.Count == 0)
        {
            if (!_hasDrawing)
            {
                _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
                return;
            }
            ClearSurface();
            _hasDrawing = false;
            _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            return;
        }
        ClearSurface();
        foreach (var stroke in _inkStrokes)
        {
            RenderStoredStroke(stroke);
        }
        _hasDrawing = _inkStrokes.Count > 0;
        _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
    }

    private void RequestInkRedraw()
    {
        if (_inkStrokes.Count == 0 && !_hasDrawing)
        {
            return;
        }
        if (_redrawPending)
        {
            return;
        }
        var throttleActive = _photoModeActive && (_photoPanning || _crossPageDragging);
        var elapsedMs = (DateTime.UtcNow - _lastInkRedrawUtc).TotalMilliseconds;
        if (throttleActive && elapsedMs < InkRedrawMinIntervalMs)
        {
            _redrawPending = true;
            var token = Interlocked.Increment(ref _inkRedrawToken);
            var delay = Math.Max(1, (int)Math.Ceiling(InkRedrawMinIntervalMs - elapsedMs));
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                try
                {
                    await System.Threading.Tasks.Task.Delay(delay).ConfigureAwait(false);
                }
                catch
                {
                    return;
                }
                var scheduled = TryBeginInvoke(() =>
                {
                    if (token != _inkRedrawToken)
                    {
                        return;
                    }
                    _redrawPending = false;
                    if (_redrawInProgress)
                    {
                        return;
                    }
                    _redrawInProgress = true;
                    try
                    {
                        _lastInkRedrawUtc = DateTime.UtcNow;
                        RedrawInkSurface();
                    }
                    finally
                    {
                        _redrawInProgress = false;
                    }
                }, DispatcherPriority.Render);
                if (!scheduled)
                {
                    _redrawPending = false;
                }
            });
            return;
        }
        _redrawPending = true;
        var directScheduled = TryBeginInvoke(() =>
        {
            _redrawPending = false;
            if (_redrawInProgress)
            {
                return;
            }
            _redrawInProgress = true;
            try
            {
                _lastInkRedrawUtc = DateTime.UtcNow;
                RedrawInkSurface();
            }
            finally
            {
                _redrawInProgress = false;
            }
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _redrawPending = false;
        }
    }

    private void SetInkContextDirty()
    {
        _pendingInkContextCheck = true;
        _refreshOrchestrator?.RequestRefresh("ink-dirty");
    }

    private void SetInkCacheDirty()
    {
        _inkCacheDirty = true;
    }

    private void MarkInkInput()
    {
        _lastInkInputUtc = DateTime.UtcNow;
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
