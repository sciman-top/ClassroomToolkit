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
using ClassroomToolkit.App.Utilities;
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
    // _activeRenderer moved to Rendering but defined here for visibility if needed? 
    // Actually, partial classes share fields so valid to define it here.
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
    private readonly List<InkSnapshot> _inkHistory = new();
    private readonly List<GlobalInkSnapshot> _globalInkHistory = new();
    private readonly DispatcherTimer _inkMonitor;
    private DispatcherTimer? _inkSidecarAutoSaveTimer;
    private readonly LatestOnlyAsyncGate _inkSidecarAutoSaveGate = new();
    private readonly Random _inkSeedRandom = new Random();
    private bool _inkRecordEnabled = true;
    private bool _inkReplayPreviousEnabled;
    private int _inkRetentionDays = 30;
    private string _inkPhotoRootPath = string.Empty;
    private bool _inkCacheEnabled = true;
    private bool _inkSaveEnabled;
    private bool _inkShowEnabled = true;
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
    
    private enum InkCacheScope
    {
        None = 0,
        Photo = 1
    }
    
    private readonly InkFinalCache _photoCache = new(80);
    private readonly InkDirtyPageCoordinator _inkDirtyPages = new();
    private readonly InkWriteAheadLogService _inkWal = new();
    private readonly InkRuntimeDiagnostics? _inkDiagnostics = InkRuntimeDiagnostics.CreateFromEnvironment();
    private int _neighborPrefetchRadiusMaxSetting = CrossPageNeighborPrefetchRadiusMax;

    // Constants
    private const double CalligraphySealStrokeWidthFactor = 0.08;
    private const int CalligraphyPreviewMinIntervalMs = 16;
    private const double CalligraphyPreviewMinDistance = 2.0;
    private const int InkInputCooldownMs = 120;
    private const int InkMonitorActiveIntervalMs = 600;
    private const int InkMonitorIdleIntervalMs = 1400;
    private const int InkIdleThresholdMs = 2500;
    private const int InkRedrawMinIntervalMs = 16;
    private const int InkSidecarAutoSaveDelayMs = 600;
    private const int InkSidecarAutoSaveRetryMax = 3;
    private const int InkSidecarAutoSaveRetryDelayMs = 900;
    private const int CrossPageNeighborPrefetchRadiusDefault = 2;
    private const int CrossPageNeighborPrefetchRadiusMin = 1;
    private const int CrossPageNeighborPrefetchRadiusMax = 4;
    private const int NeighborInkCacheLimit = 10;

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
    
    // Core Helpers
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
}
