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
    private CalligraphyRenderMode _calligraphyRenderMode = CalligraphyRenderMode.Clarity;
    private WhiteboardBrushPreset _whiteboardPreset = WhiteboardBrushPreset.Balanced;
    private ClassroomWritingMode _classroomWritingMode = ClassroomWritingMode.Balanced;
    private int _inkRedrawToken;
    private DateTime _lastInkRedrawUtc = InkRuntimeTimingDefaults.UnsetTimestampUtc;

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
    private DateTime _lastCalligraphyPreviewUtc = InkRuntimeTimingDefaults.UnsetTimestampUtc;
    private WpfPoint? _lastCalligraphyPreviewPoint;
    private double _calligraphyPreviewMinDistance = CalligraphyPreviewMinDistanceDefault;
    private double _stylusPseudoPressureLowThreshold = StylusPseudoPressureLowThresholdDefault;
    private double _stylusPseudoPressureHighThreshold = StylusPseudoPressureHighThresholdDefault;
    private readonly StylusPressureSignalAnalyzer _stylusPressureAnalyzer = new();
    private readonly StylusPressureCurveCalibrator _stylusPressureCalibrator = new();
    private readonly StylusDeviceAdaptiveProfiler _stylusDeviceAdaptiveProfiler = new();
    private bool _pendingAdaptiveRendererRefresh;
    private BrushInputSample? _lastBrushInputSample;
    private BrushInputSample? _pendingCrossPageBrushContinuationSample;
    private bool _pendingCrossPageBrushReplayCurrentInput;
    private bool _activeBrushStrokeUsesCrossPageContinuation;
    private bool _suppressImmediatePhotoInkRedraw;
    private bool _suppressCrossPageVisualSync;
    private Vector _lastBrushVelocityDipPerSec = new Vector(0, 0);
    private int _brushPredictionHorizonMs = 8;
    private readonly IInkRendererFactory _inkRendererFactory;
    private const double BrushPredictionMaxDistanceDip = InkPredictionDefaults.MaxDistanceDip;
    
    // Ink History & Cache
    private readonly List<InkStrokeData> _inkStrokes = new();
    private readonly List<InkSnapshot> _inkHistory = new();
    private readonly List<GlobalInkSnapshot> _globalInkHistory = new();
    private readonly DispatcherTimer _inkMonitor;
    private DispatcherTimer? _inkSidecarAutoSaveTimer;
    private readonly LatestOnlyAsyncGate _inkSidecarAutoSaveGate = new();
    private bool _inkRecordEnabled = true;
    private bool _inkReplayPreviousEnabled;
    private int _inkRetentionDays = 30;
    private string _inkPhotoRootPath = string.Empty;
    private bool _inkCacheEnabled = true;
    private bool _inkSaveEnabled;
    private bool _inkShowEnabled = true;
    private DateTime _lastInkInputUtc = InkRuntimeTimingDefaults.UnsetTimestampUtc;
    private bool _pendingInkContextCheck;
    
    // Pools & Buffers
    private static readonly ArrayPool<byte> PixelPool = ArrayPool<byte>.Shared;
    private const int HistoryLimit = InkCacheRuntimeDefaults.HistoryLimit;
    private const long MaxHistoryMemoryBytes = InkCacheRuntimeDefaults.MaxHistoryMemoryBytes;
    private long _currentHistoryMemoryBytes;
    private const int InkNoiseTileCacheLimit = InkCacheRuntimeDefaults.NoiseTileCacheLimit;
    private const int InkSolidBrushCacheLimit = InkCacheRuntimeDefaults.SolidBrushCacheLimit;
    private const int InkPenCacheLimit = InkCacheRuntimeDefaults.PenCacheLimit;
    private static readonly object InkNoiseTileCacheLock = new();
    private static readonly Dictionary<InkNoiseTileKey, InkNoiseTileEntry> InkNoiseTileCache = new();
    private static readonly LinkedList<InkNoiseTileKey> InkNoiseTileOrder = new();
    private readonly Dictionary<int, SolidColorBrush> _inkSolidBrushCache = new();
    private readonly Dictionary<InkPenCacheKey, MediaPen> _inkPenCache = new();
    private byte[]? _clearSurfaceBuffer;
    private int _clearSurfaceBufferSize;
    private byte[]? _compositeSurfaceBuffer;
    private int _compositeSurfaceBufferSize;
    private readonly DrawingVisual _scratchRenderVisual = new();

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
    private const double CalligraphySealStrokeWidthFactor = CalligraphyRenderingDefaults.SealStrokeWidthFactor;
    private const int CalligraphyPreviewMinIntervalMs = InkRuntimeTimingDefaults.CalligraphyPreviewMinIntervalMs;
    private const double CalligraphyPreviewMinDistanceDefault = ClassroomWritingModeTuner.DefaultCalligraphyPreviewMinDistance;
    private const double StylusPseudoPressureLowThresholdDefault = ClassroomWritingModeTuner.DefaultPseudoPressureLowThreshold;
    private const double StylusPseudoPressureHighThresholdDefault = ClassroomWritingModeTuner.DefaultPseudoPressureHighThreshold;
    private const int InkInputCooldownMs = InkRuntimeTimingDefaults.InputCooldownMs;
    private const int InkMonitorActiveIntervalMs = InkRuntimeTimingDefaults.MonitorActiveIntervalMs;
    private const int InkMonitorIdleIntervalMs = InkRuntimeTimingDefaults.MonitorIdleIntervalMs;
    private const int InkIdleThresholdMs = InkRuntimeTimingDefaults.IdleThresholdMs;
    private const int InkRedrawMinIntervalMs = InkRuntimeTimingDefaults.RedrawMinIntervalMs;
    private const int InkSidecarAutoSaveDelayMs = InkRuntimeTimingDefaults.SidecarAutoSaveDelayMs;
    private const int InkSidecarAutoSaveRetryMax = InkRuntimeTimingDefaults.SidecarAutoSaveRetryMax;
    private const int InkSidecarAutoSaveRetryDelayMs = InkRuntimeTimingDefaults.SidecarAutoSaveRetryDelayMs;
    private const int CrossPageNeighborPrefetchRadiusDefault = CrossPageNeighborPrefetchDefaults.RadiusDefault;
    private const int CrossPageNeighborPrefetchRadiusMin = CrossPageNeighborPrefetchDefaults.RadiusMin;
    private const int CrossPageNeighborPrefetchRadiusMax = CrossPageNeighborPrefetchDefaults.RadiusMax;
    private const int NeighborInkCacheLimit = CrossPageNeighborPrefetchDefaults.NeighborInkCacheLimit;
    private const double CalligraphyDegradeAreaThreshold = CalligraphyRenderingDefaults.DegradeAreaThreshold;
    private const int CalligraphyDegradeLayerThreshold = CalligraphyRenderingDefaults.DegradeLayerThreshold;
    private const int CalligraphyMaxRibbonLayersNormal = CalligraphyRenderingDefaults.MaxRibbonLayersNormal;
    private const int CalligraphyMaxRibbonLayersDegraded = CalligraphyRenderingDefaults.MaxRibbonLayersDegraded;
    private const int CalligraphyMaxBloomLayersNormal = CalligraphyRenderingDefaults.MaxBloomLayersNormal;
    private const int CalligraphyMaxBloomLayersDegraded = CalligraphyRenderingDefaults.MaxBloomLayersDegraded;
    private const int CalligraphyAdaptiveLevelMax = CalligraphyRenderingDefaults.AdaptiveLevelMax;
    private const double CalligraphyAdaptiveHighCostMs = CalligraphyRenderingDefaults.AdaptiveHighCostMs;
    private const double CalligraphyAdaptiveLowCostMs = CalligraphyRenderingDefaults.AdaptiveLowCostMs;
    private const double CalligraphyAdaptiveCostEmaAlpha = CalligraphyRenderingDefaults.AdaptiveCostEmaAlpha;
    private const int CalligraphyAdaptiveAdjustMinIntervalMs = InkRuntimeTimingDefaults.CalligraphyAdaptiveAdjustMinIntervalMs;
    private const int CalligraphyAdaptiveAreaThresholdStep = CalligraphyRenderingDefaults.AdaptiveAreaThresholdStep;
    private const int CalligraphyAdaptiveLayerThresholdStep = CalligraphyRenderingDefaults.AdaptiveLayerThresholdStep;
    private static readonly bool CalligraphySinglePassCompositeEnabled = true;
    private static readonly bool CalligraphySinglePassTextureMaskEnabled = false;
    private static readonly bool CalligraphySinglePassSealEnabled = false;

    private double _calligraphyBatchCostEmaMs = 4.0;
    private int _calligraphyAdaptiveLevel;
    private DateTime _lastCalligraphyAdaptiveAdjustUtc = InkRuntimeTimingDefaults.UnsetTimestampUtc;

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

    private byte[] GetCompositeSurfaceBuffer(int requiredLength)
    {
        if (_compositeSurfaceBuffer == null || _compositeSurfaceBufferSize < requiredLength)
        {
            _compositeSurfaceBuffer = new byte[requiredLength];
            _compositeSurfaceBufferSize = requiredLength;
        }
        return _compositeSurfaceBuffer;
    }
    
    private void BlendSourceOver(Int32Rect rect, byte[] srcPixels, int srcStride)
    {
        if (_rasterSurface == null)
        {
            return;
        }
        var destStride = rect.Width * 4;
        var destLength = destStride * rect.Height;
        var destPixels = GetCompositeSurfaceBuffer(destLength);
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
                if (srcA == 255 || destPixels[d + 3] == 0)
                {
                    destPixels[d] = srcPixels[i];
                    destPixels[d + 1] = srcPixels[i + 1];
                    destPixels[d + 2] = srcPixels[i + 2];
                    destPixels[d + 3] = srcA;
                    continue;
                }

                destPixels[d] = (byte)(srcPixels[i] + destPixels[d] * invA / 255);
                destPixels[d + 1] = (byte)(srcPixels[i + 1] + destPixels[d + 1] * invA / 255);
                destPixels[d + 2] = (byte)(srcPixels[i + 2] + destPixels[d + 2] * invA / 255);
                destPixels[d + 3] = (byte)(srcA + destPixels[d + 3] * invA / 255);
            }
        }
        _rasterSurface.WritePixels(rect, destPixels, destStride, 0);
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
                TryScheduleDrain();
            }
        }

        private void TryScheduleDrain()
        {
            try
            {
                _dispatcher.BeginInvoke(new Action(Drain), DispatcherPriority.Background);
            }
            catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
            {
                Interlocked.Exchange(ref _refreshRunning, 0);
                System.Diagnostics.Debug.WriteLine(
                    $"[InkRefresh] BeginInvoke failed: {ex.GetType().Name} - {ex.Message}");
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
                        TryScheduleDrain();
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
        private DateTime _lastLogUtc = InkRuntimeTimingDefaults.UnsetTimestampUtc;

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

            if (_count < 30 && (GetCurrentUtcTimestamp() - _lastLogUtc).TotalSeconds < 30)
            {
                return;
            }
            if (_count % 30 != 0 && (GetCurrentUtcTimestamp() - _lastLogUtc).TotalSeconds < 30)
            {
                return;
            }

            var avg = _total / Math.Max(1, _count);
            var uiRatio = _count == 0 ? 0 : (double)_uiThreadSamples / _count * 100.0;
            System.Diagnostics.Debug.WriteLine(
                $"[Perf] {_name}: avg={avg:F2}ms max={_max:F2}ms samples={_count} ui={uiRatio:F0}%");
            _lastLogUtc = GetCurrentUtcTimestamp();
        }
    }
}

