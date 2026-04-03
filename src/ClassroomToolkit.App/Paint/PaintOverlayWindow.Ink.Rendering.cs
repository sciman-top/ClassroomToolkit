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

    private void RenderStoredStroke(InkStrokeData stroke)
    {
        var photoInkModeActive = IsPhotoInkModeActive();
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

        if (!photoInkModeActive && stroke.CachedBounds.HasValue)
        {
             var bounds = stroke.CachedBounds.Value;
             if (bounds.Right < 0 || bounds.Bottom < 0 || bounds.Left > _surfacePixelWidth || bounds.Top > _surfacePixelHeight)
             {
                 return;
             }
        }
        
        var usePhotoTransform = photoInkModeActive && ReferenceEquals(RasterImage.RenderTransform, _photoContentTransform);
        var renderGeometry = usePhotoTransform
            ? geometry
            : (photoInkModeActive ? ToScreenGeometry(geometry) : geometry);
        if (renderGeometry == null)
        {
            return;
        }

        if (_activeInkRedrawClipBoundsDip.HasValue
            && !renderGeometry.Bounds.IntersectsWith(_activeInkRedrawClipBoundsDip.Value))
        {
            return;
        }
        
        if (photoInkModeActive
            && !PhotoInkViewportIntersectionPolicy.ShouldRender(
                photoInkModeActive,
                usePhotoTransform,
                renderGeometry.Bounds,
                _photoContentTransform?.Value ?? Matrix.Identity,
                ResolveInkViewportBoundsDip()))
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
        if (CalligraphySinglePassCompositeEnabled)
        {
            RenderCalligraphyComposite(
                renderGeometry,
                color,
                stroke.BrushSize,
                inkFlow,
                strokeDirection,
                stroke.CalligraphyRenderMode,
                stroke.CalligraphySealEnabled,
                stroke.CalligraphyInkBloomEnabled,
                ribbonLayers: null,
                blooms: null,
                suppressOverlays,
                stroke.MaskSeed);
            return;
        }

        List<(Geometry Geometry, double Opacity)>? ribbons = null;
        if (!suppressOverlays && stroke.Ribbons.Count > 0)
        {
            ribbons = new List<(Geometry Geometry, double Opacity)>();
            foreach (var ribbon in stroke.Ribbons)
            {
                var ribbonGeometry = InkGeometrySerializer.Deserialize(ribbon.GeometryPath);
                if (ribbonGeometry == null)
                {
                    continue;
                }
                var renderRibbon = usePhotoTransform
                    ? ribbonGeometry
                    : (photoInkModeActive ? ToScreenGeometry(ribbonGeometry) : ribbonGeometry);
                if (renderRibbon == null)
                {
                    continue;
                }
                ribbons.Add((renderRibbon, ribbon.Opacity));
            }
        }
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
                var renderBloom = usePhotoTransform
                    ? bloomGeometry
                    : (photoInkModeActive ? ToScreenGeometry(bloomGeometry) : bloomGeometry);
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
            stroke.CalligraphyRenderMode,
            stroke.CalligraphySealEnabled,
            stroke.CalligraphyInkBloomEnabled,
            ribbons,
            blooms,
            suppressOverlays,
            stroke.MaskSeed);
    }


    private void RenderInkLayers(Geometry geometry, MediaColor color, double inkFlow, double ribbonOpacity, Vector? strokeDirection)
    {
        var solidBrush = GetCachedSolidBrush(color, Math.Clamp(ribbonOpacity, 0.1, 1.0));
        var mask = IsInkMaskEligible(geometry)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection)
            : null;
        RenderAndBlend(geometry, solidBrush, null, erase: false, mask);
    }

    private Rect ResolveInkViewportBoundsDip()
    {
        var width = OverlayRoot.ActualWidth;
        if (width <= 0)
        {
            width = ActualWidth;
        }
        if (width <= 0)
        {
            width = _surfaceDpiX > 0
                ? _surfacePixelWidth * 96.0 / _surfaceDpiX
                : _surfacePixelWidth;
        }

        var height = OverlayRoot.ActualHeight;
        if (height <= 0)
        {
            height = ActualHeight;
        }
        if (height <= 0)
        {
            height = _surfaceDpiY > 0
                ? _surfacePixelHeight * 96.0 / _surfaceDpiY
                : _surfacePixelHeight;
        }

        if (width <= 0 || height <= 0)
        {
            return Rect.Empty;
        }

        return new Rect(0, 0, width, height);
    }

    private void RenderInkCore(Geometry geometry, MediaColor color, bool enableSeal)
    {
        var brush = GetCachedSolidBrush(color);
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
        var pen = GetCachedPen(color, sealWidth);
        RenderAndBlend(geometry, null, pen, erase: false, null);
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
        var pen = GetCachedPen(color, sealWidth);
        RenderAndBlend(geometry, null, pen, erase: false, null);
    }

    private void RenderInkEdge(Geometry coreGeometry, MediaColor color, double inkFlow, Vector? strokeDirection)
    {
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(_brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var pen = GetCachedPen(color, edgeWidth, edgeOpacity, PenLineJoin.Round, PenLineCap.Round, PenLineCap.Round);
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

    private readonly struct InkPenCacheKey : IEquatable<InkPenCacheKey>
    {
        public InkPenCacheKey(int colorKey, int widthMilli, PenLineJoin lineJoin, PenLineCap startCap, PenLineCap endCap)
        {
            ColorKey = colorKey;
            WidthMilli = widthMilli;
            LineJoin = lineJoin;
            StartCap = startCap;
            EndCap = endCap;
        }

        public int ColorKey { get; }
        public int WidthMilli { get; }
        public PenLineJoin LineJoin { get; }
        public PenLineCap StartCap { get; }
        public PenLineCap EndCap { get; }

        public bool Equals(InkPenCacheKey other)
        {
            return ColorKey == other.ColorKey
                && WidthMilli == other.WidthMilli
                && LineJoin == other.LineJoin
                && StartCap == other.StartCap
                && EndCap == other.EndCap;
        }

        public override bool Equals(object? obj)
        {
            return obj is InkPenCacheKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ColorKey, WidthMilli, (int)LineJoin, (int)StartCap, (int)EndCap);
        }
    }

    private SolidColorBrush GetCachedSolidBrush(MediaColor baseColor, double opacity = 1.0)
    {
        var color = ApplyOpacity(baseColor, opacity);
        int key = PackColorKey(color);
        if (_inkSolidBrushCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_inkSolidBrushCache.Count >= InkSolidBrushCacheLimit)
        {
            _inkSolidBrushCache.Clear();
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _inkSolidBrushCache[key] = brush;
        return brush;
    }

    private MediaPen GetCachedPen(
        MediaColor baseColor,
        double width,
        double opacity = 1.0,
        PenLineJoin lineJoin = PenLineJoin.Round,
        PenLineCap startCap = PenLineCap.Round,
        PenLineCap endCap = PenLineCap.Round)
    {
        var color = ApplyOpacity(baseColor, opacity);
        int colorKey = PackColorKey(color);
        int widthMilli = Math.Max(
            InkRenderingCacheDefaults.PenWidthMinMilli,
            (int)Math.Round(width * InkRenderingCacheDefaults.PenWidthQuantizeScale));
        var key = new InkPenCacheKey(colorKey, widthMilli, lineJoin, startCap, endCap);
        if (_inkPenCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_inkPenCache.Count >= InkPenCacheLimit)
        {
            _inkPenCache.Clear();
        }

        var pen = new MediaPen(GetCachedSolidBrush(color), widthMilli / InkRenderingCacheDefaults.PenWidthQuantizeScale)
        {
            LineJoin = lineJoin,
            StartLineCap = startCap,
            EndLineCap = endCap,
            MiterLimit = 2.4
        };
        pen.Freeze();
        _inkPenCache[key] = pen;
        return pen;
    }

    private static MediaColor ApplyOpacity(MediaColor color, double opacity)
    {
        byte alpha = (byte)Math.Clamp(Math.Round(color.A * Math.Clamp(opacity, 0.0, 1.0)), 0.0, 255.0);
        return MediaColor.FromArgb(alpha, color.R, color.G, color.B);
    }

    private static int PackColorKey(MediaColor color)
    {
        return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
    }

    private static int ResolveLayerStep(int layerCount, int maxLayers)
    {
        if (layerCount <= 0 || maxLayers <= 0 || layerCount <= maxLayers)
        {
            return 1;
        }
        return Math.Max(1, (int)Math.Ceiling(layerCount / (double)maxLayers));
    }

    private void UpdateCalligraphyAdaptiveLevel(double batchElapsedMs)
    {
        _calligraphyBatchCostEmaMs = _calligraphyBatchCostEmaMs * (1.0 - CalligraphyAdaptiveCostEmaAlpha)
            + batchElapsedMs * CalligraphyAdaptiveCostEmaAlpha;

        var nowUtc = GetCurrentUtcTimestamp();
        if ((nowUtc - _lastCalligraphyAdaptiveAdjustUtc).TotalMilliseconds < CalligraphyAdaptiveAdjustMinIntervalMs)
        {
            return;
        }

        _lastCalligraphyAdaptiveAdjustUtc = nowUtc;
        if (_calligraphyBatchCostEmaMs > CalligraphyAdaptiveHighCostMs)
        {
            _calligraphyAdaptiveLevel = Math.Min(CalligraphyAdaptiveLevelMax, _calligraphyAdaptiveLevel + 1);
            return;
        }

        if (_calligraphyBatchCostEmaMs < CalligraphyAdaptiveLowCostMs)
        {
            _calligraphyAdaptiveLevel = Math.Max(0, _calligraphyAdaptiveLevel - 1);
        }
    }

    private void RenderCalligraphyComposite(
        Geometry geometry,
        MediaColor color,
        double brushSize,
        double inkFlow,
        Vector? strokeDirection,
        CalligraphyRenderMode renderMode,
        bool sealEnabled,
        bool bloomEnabled,
        IReadOnlyList<(Geometry Geometry, double Opacity)>? ribbonLayers,
        IEnumerable<(Geometry Geometry, double Opacity)>? blooms,
        bool suppressOverlays,
        int? maskSeed)
    {
        List<(Geometry Geometry, double Opacity)>? bloomLayers = null;
        if (!suppressOverlays && bloomEnabled && blooms != null)
        {
            bloomLayers = new List<(Geometry Geometry, double Opacity)>();
            foreach (var bloom in blooms)
            {
                if (bloom.Geometry == null || bloom.Geometry.Bounds.IsEmpty)
                {
                    continue;
                }
                bloomLayers.Add(bloom);
            }
        }

        bool inkMode = renderMode == CalligraphyRenderMode.Ink;
        bool overlaysEnabled = !suppressOverlays && inkMode;
        if (CalligraphySinglePassCompositeEnabled)
        {
            int singlePassSeededMaskValue = maskSeed ?? ResolveDeterministicMaskSeed(geometry, color, brushSize, renderMode);
            bool singlePassMaskEligible = (inkMode || CalligraphySinglePassTextureMaskEnabled)
                && IsInkMaskEligible(geometry, brushSize);
            MediaBrush? coreMask = null;
            if (singlePassMaskEligible)
            {
                coreMask = BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, singlePassSeededMaskValue);
            }

            int singlePassCommandCapacity = 1;
            if (overlaysEnabled)
            {
                singlePassCommandCapacity++;
            }
            if (overlaysEnabled && sealEnabled && CalligraphySinglePassSealEnabled)
            {
                singlePassCommandCapacity++;
            }

            var singlePassCommands = new List<DrawCommand>(singlePassCommandCapacity);
            var singlePassCoreBrush = GetCachedSolidBrush(color, opacity: 1.0);
            singlePassCommands.Add(new DrawCommand(geometry, singlePassCoreBrush, null, coreMask, null));

            if (overlaysEnabled)
            {
                double accumulationOpacity = Math.Clamp(Lerp(0.04, 0.1, Math.Clamp(inkFlow, 0.0, 1.0)), 0.03, 0.11);
                var accumulationBrush = GetCachedSolidBrush(color, opacity: accumulationOpacity);
                singlePassCommands.Add(new DrawCommand(geometry, accumulationBrush, null, coreMask, null));
            }

            if (overlaysEnabled && sealEnabled && CalligraphySinglePassSealEnabled)
            {
                double sealWidth = Math.Max(brushSize * CalligraphySealStrokeWidthFactor, 0.6);
                if (sealWidth > 0)
                {
                    var sealPen = GetCachedPen(
                        color,
                        sealWidth,
                        opacity: 0.14,
                        lineJoin: PenLineJoin.Round,
                        startCap: PenLineCap.Round,
                        endCap: PenLineCap.Round);
                    singlePassCommands.Add(new DrawCommand(geometry, null, sealPen, null, null));
                }
            }

            var renderSwSinglePass = Stopwatch.StartNew();
            RenderAndBlendBatch(singlePassCommands);
            UpdateCalligraphyAdaptiveLevel(renderSwSinglePass.Elapsed.TotalMilliseconds);
            return;
        }

        suppressOverlays = suppressOverlays || !inkMode;
        int ribbonLayerCount = suppressOverlays ? 0 : (ribbonLayers?.Count ?? 0);
        int bloomLayerCount = suppressOverlays ? 0 : (bloomLayers?.Count ?? 0);
        double geometryArea = geometry.Bounds.IsEmpty ? 0.0 : geometry.Bounds.Width * geometry.Bounds.Height;
        double adaptiveAreaThreshold = Math.Max(60000.0, CalligraphyDegradeAreaThreshold - _calligraphyAdaptiveLevel * CalligraphyAdaptiveAreaThresholdStep);
        int adaptiveLayerThreshold = Math.Max(8, CalligraphyDegradeLayerThreshold - _calligraphyAdaptiveLevel * CalligraphyAdaptiveLayerThresholdStep);
        bool degradeQuality = !suppressOverlays
            && (geometryArea >= adaptiveAreaThreshold
                || (ribbonLayerCount + bloomLayerCount) >= adaptiveLayerThreshold);

        int maxRibbonLayers = degradeQuality
            ? Math.Max(4, CalligraphyMaxRibbonLayersDegraded - _calligraphyAdaptiveLevel * 2)
            : CalligraphyMaxRibbonLayersNormal;
        int maxBloomLayers = degradeQuality
            ? Math.Max(0, CalligraphyMaxBloomLayersDegraded - _calligraphyAdaptiveLevel * 2)
            : CalligraphyMaxBloomLayersNormal;
        int ribbonStep = ResolveLayerStep(ribbonLayerCount, maxRibbonLayers);
        int bloomStep = ResolveLayerStep(bloomLayerCount, maxBloomLayers);

        int estimatedCommands = 2; // core + edge
        if (!suppressOverlays && sealEnabled)
        {
            estimatedCommands++;
        }
        if (!suppressOverlays && maxBloomLayers > 0 && bloomLayerCount > 0)
        {
            estimatedCommands += Math.Max(1, bloomLayerCount / bloomStep);
        }
        if (!suppressOverlays)
        {
            estimatedCommands += Math.Max(1, ribbonLayerCount > 0 ? ribbonLayerCount / ribbonStep : 1);
        }
        var commands = new List<DrawCommand>(Math.Max(estimatedCommands, 4));

        if (!suppressOverlays && maxBloomLayers > 0 && bloomLayers != null && bloomLayers.Count > 0)
        {
            for (int i = 0; i < bloomLayers.Count; i += bloomStep)
            {
                var bloom = bloomLayers[i];
                var bloomBrush = GetCachedSolidBrush(color, bloom.Opacity);
                commands.Add(new DrawCommand(bloom.Geometry, bloomBrush, null, null, geometry));
            }
        }

        // Speed->ink tone coupling (via inkFlow): faster strokes are slightly drier/lighter,
        // slower strokes are wetter/darker.
        double coreOpacity = Math.Clamp(Lerp(0.84, 1.0, Math.Clamp(inkFlow, 0.0, 1.0)), 0.78, 1.0);
        var coreBrush = GetCachedSolidBrush(color, coreOpacity);
        commands.Add(new DrawCommand(geometry, coreBrush, null, null, null));

        if (!suppressOverlays && sealEnabled)
        {
            double sealWidth = Math.Max(brushSize * CalligraphySealStrokeWidthFactor, 0.6);
            if (sealWidth > 0)
            {
                var sealPen = GetCachedPen(color, sealWidth);
                commands.Add(new DrawCommand(geometry, null, sealPen, null, null));
            }
        }

        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgePen = GetCachedPen(color, edgeWidth, edgeOpacity, PenLineJoin.Round, PenLineCap.Round, PenLineCap.Round);
        int seededMaskValue = maskSeed ?? ResolveDeterministicMaskSeed(geometry, color, brushSize, renderMode);
        bool maskEligible = IsInkMaskEligible(geometry, brushSize);
        MediaBrush? sharedMask = null;
        if (maskEligible)
        {
            sharedMask = BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, seededMaskValue);
        }

        var edgeMask = sharedMask;
        commands.Add(new DrawCommand(geometry, null, edgePen, edgeMask, null));

        if (!suppressOverlays)
        {
            var ribbonMask = sharedMask;

            if (ribbonLayers != null && ribbonLayers.Count > 0)
            {
                for (int i = 0; i < ribbonLayers.Count; i += ribbonStep)
                {
                    var ribbon = ribbonLayers[i];
                    if (ribbon.Geometry == null || ribbon.Geometry.Bounds.IsEmpty)
                    {
                        continue;
                    }
                    var ribbonFlowFactor = Math.Clamp(Lerp(0.86, 1.06, Math.Clamp(inkFlow, 0.0, 1.0)), 0.78, 1.12);
                    var ribbonOpacity = Math.Clamp(ribbon.Opacity * 0.35 * ribbonFlowFactor, 0.06, 0.45);
                    var ribbonBrush = GetCachedSolidBrush(color, ribbonOpacity);
                    commands.Add(new DrawCommand(ribbon.Geometry, ribbonBrush, null, ribbonMask, null));
                }
            }
            else
            {
                var ribbonBrush = GetCachedSolidBrush(color, Math.Clamp(0.28, 0.1, 1.0));
                commands.Add(new DrawCommand(geometry, ribbonBrush, null, ribbonMask, null));
            }
        }

        var renderSw = Stopwatch.StartNew();
        RenderAndBlendBatch(commands);
        UpdateCalligraphyAdaptiveLevel(renderSw.Elapsed.TotalMilliseconds);
    }

    private bool ShouldSuppressCalligraphyOverlays()
    {
        // In photo/PDF mode prioritize stroke stability and latency over decorative overlays.
        return IsPhotoInkModeActive() || _brushOpacity < _calligraphyOverlayOpacityThreshold;
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

    private static int ResolveDeterministicMaskSeed(
        Geometry geometry,
        MediaColor color,
        double brushSize,
        CalligraphyRenderMode renderMode)
    {
        var bounds = geometry.Bounds;
        uint hash = 2166136261u;
        hash = Fnv1aMask(hash, QuantizeMask(bounds.X, 100.0));
        hash = Fnv1aMask(hash, QuantizeMask(bounds.Y, 100.0));
        hash = Fnv1aMask(hash, QuantizeMask(bounds.Width, 100.0));
        hash = Fnv1aMask(hash, QuantizeMask(bounds.Height, 100.0));
        hash = Fnv1aMask(hash, QuantizeMask(brushSize, 1000.0));
        hash = Fnv1aMask(hash, color.A << 24 | color.R << 16 | color.G << 8 | color.B);
        hash = Fnv1aMask(hash, (int)renderMode);
        int seed = unchecked((int)hash);
        return seed == 0 ? 17 : seed;
    }

    private static uint Fnv1aMask(uint hash, int value)
    {
        unchecked
        {
            hash ^= (uint)value;
            hash *= 16777619u;
        }

        return hash;
    }

    private static int QuantizeMask(double value, double scale)
    {
        if (!double.IsFinite(value))
        {
            return 0;
        }

        return (int)Math.Round(value * scale);
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

    private bool TryResolveInkRedrawClip(
        out Int32Rect clipPixelRect,
        out Rect clipBoundsDip)
    {
        clipPixelRect = default;
        clipBoundsDip = Rect.Empty;
        if (RasterImage.Clip is not RectangleGeometry clipGeometry)
        {
            return false;
        }

        var raw = clipGeometry.Rect;
        if (raw.IsEmpty || raw.Width <= 0 || raw.Height <= 0)
        {
            return false;
        }

        clipBoundsDip = raw;
        if (!InkRedrawClipPolicy.TryResolvePixelClip(
                raw,
                _surfacePixelWidth,
                _surfacePixelHeight,
                _surfaceDpiX,
                _surfaceDpiY,
                out clipPixelRect))
        {
            return false;
        }
        return true;
    }



    
    private void RedrawInkSurface()
    {
        var redrawSw = Stopwatch.StartNew();
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("redraw-enter", $"strokes={_inkStrokes.Count}");
        }
        EnsureRasterSurface();
        if (_rasterSurface == null)
        {
            _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("redraw-exit", "surface-null");
            }
            return;
        }
        if (_inkStrokes.Count == 0)
        {
            _lastInkRedrawClipPixelRect = null;
            _activeInkRedrawClipBoundsDip = null;
            if (!_hasDrawing)
            {
                _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("redraw-exit", "empty-noop");
                }
                return;
            }
            // In non-record mode we may only have rasterized strokes (no vector stroke list).
            // Avoid clearing the surface during redraw, otherwise freshly written ink disappears on pointer-up.
            if (!_inkRecordEnabled)
            {
                _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("redraw-exit", "raster-only");
                }
                return;
            }
            ClearSurface();
            _hasDrawing = false;
            _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("redraw-exit", "empty-cleared");
            }
            return;
        }

        var usePartialClear = false;
        if (TryResolveInkRedrawClip(out var clipPixelRect, out var clipBoundsDip)
            && InkRedrawClipPolicy.ShouldUsePartialClear(
                clipAvailable: true,
                clipPixelRect: clipPixelRect,
                lastClipPixelRect: _lastInkRedrawClipPixelRect))
        {
            _activeInkRedrawClipBoundsDip = clipBoundsDip;
            usePartialClear = true;
            ClearSurface(clipPixelRect);
        }
        else
        {
            _activeInkRedrawClipBoundsDip = null;
            ClearSurface();
        }

        foreach (var stroke in _inkStrokes)
        {
            RenderStoredStroke(stroke);
        }
        _activeInkRedrawClipBoundsDip = null;
        _lastInkRedrawClipPixelRect = usePartialClear
            ? _lastInkRedrawClipPixelRect
            : (TryResolveInkRedrawClip(out var latestClipPixelRect, out _) ? latestClipPixelRect : null);
        _hasDrawing = _inkStrokes.Count > 0;
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: IsPhotoInkModeActive());
        _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
        TrackInkRedrawTelemetry(usePartialClear, redrawSw.Elapsed.TotalMilliseconds);
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("redraw-exit", $"ms={redrawSw.Elapsed.TotalMilliseconds:F2}");
        }
    }

    private void TrackInkRedrawTelemetry(bool partialClear, double elapsedMs)
    {
        if (!InkRedrawTelemetryEnabled)
        {
            return;
        }

        if (!double.IsFinite(elapsedMs) || elapsedMs < 0)
        {
            return;
        }

        _inkRedrawTelemetryTotalSamples++;
        if (partialClear)
        {
            _inkRedrawTelemetryPartialSamples++;
        }

        InkRedrawTelemetryPolicy.AppendSample(
            _inkRedrawTelemetryAllWindow,
            elapsedMs,
            InkRedrawTelemetryWindowSize);
        InkRedrawTelemetryPolicy.AppendSample(
            partialClear ? _inkRedrawTelemetryPartialWindow : _inkRedrawTelemetryFullWindow,
            elapsedMs,
            InkRedrawTelemetryWindowSize);

        var nowUtc = GetCurrentUtcTimestamp();
        if (!InkRedrawTelemetryPolicy.ShouldEmitLog(
                _inkRedrawTelemetryTotalSamples,
                nowUtc,
                _lastInkRedrawTelemetryLogUtc,
                InkRedrawTelemetrySampleStride,
                InkRedrawTelemetryLogMinIntervalSeconds))
        {
            return;
        }

        var hitRate = _inkRedrawTelemetryTotalSamples <= 0
            ? 0
            : (double)_inkRedrawTelemetryPartialSamples / _inkRedrawTelemetryTotalSamples * 100.0;
        var allP50 = InkRedrawTelemetryPolicy.Percentile(_inkRedrawTelemetryAllWindow, 0.5);
        var allP95 = InkRedrawTelemetryPolicy.Percentile(_inkRedrawTelemetryAllWindow, 0.95);
        var partialP95 = InkRedrawTelemetryPolicy.Percentile(_inkRedrawTelemetryPartialWindow, 0.95);
        var fullP95 = InkRedrawTelemetryPolicy.Percentile(_inkRedrawTelemetryFullWindow, 0.95);
        _inkDiagnostics?.OnInkRedrawTelemetry(
            _inkRedrawTelemetryTotalSamples,
            hitRate,
            _inkRedrawTelemetryAllWindow.Count,
            InkRedrawTelemetryWindowSize,
            allP50,
            allP95,
            partialP95,
            fullP95);
        _lastInkRedrawTelemetryLogUtc = nowUtc;
    }

    private void RequestInkRedraw()
    {
        if (_inkStrokes.Count == 0 && !_hasDrawing)
        {
            return;
        }
        var requestedStamp = CaptureInkRedrawVersionStamp();
        if (_redrawPending)
        {
            _pendingInkRedrawVersionStamp = MergeInkRedrawVersionStamp(_pendingInkRedrawVersionStamp, requestedStamp);
            return;
        }
        _pendingInkRedrawVersionStamp = requestedStamp;
        var throttleActive = IsPhotoInkModeActive() && IsCrossPagePanOrDragActive();
        var elapsedMs = (GetCurrentUtcTimestamp() - _lastInkRedrawUtc).TotalMilliseconds;
        _inkDiagnostics?.OnRedrawRequested(throttleActive && elapsedMs < InkRedrawMinIntervalMs);
        if (throttleActive && elapsedMs < InkRedrawMinIntervalMs)
        {
            _redrawPending = true;
            var token = Interlocked.Increment(ref _inkRedrawToken);
            var delay = Math.Max(
                InkRuntimeTimingDefaults.RedrawDispatchDelayMinMs,
                (int)Math.Ceiling(InkRedrawMinIntervalMs - elapsedMs));
            var lifecycleToken = _overlayLifecycleCancellation.Token;
            _ = SafeTaskRunner.Run(
                "PaintOverlayWindow.RequestInkRedraw.Throttled",
                async cancellationToken =>
                {
                    await System.Threading.Tasks.Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    var scheduled = TryBeginInvoke(() =>
                    {
                        if (token != _inkRedrawToken)
                        {
                            return;
                        }
                        var scheduledStamp = _pendingInkRedrawVersionStamp;
                        _redrawPending = false;
                        _pendingInkRedrawVersionStamp = default;
                        if (!IsInkRedrawVersionCurrent(scheduledStamp))
                        {
                            RequestInkRedraw();
                            return;
                        }
                        if (_redrawInProgress)
                        {
                            return;
                        }
                        _redrawInProgress = true;
                        try
                        {
                            _lastInkRedrawUtc = GetCurrentUtcTimestamp();
                            RedrawInkSurface();
                            OnInkRedrawCompleted();
                            _inkDiagnostics?.OnRedrawCompleted((GetCurrentUtcTimestamp() - _lastInkRedrawUtc).TotalMilliseconds);
                        }
                        finally
                        {
                            _redrawInProgress = false;
                        }
                    }, DispatcherPriority.Render);
                    if (!scheduled)
                    {
                        _redrawPending = false;
                        _pendingInkRedrawVersionStamp = default;
                    }
                },
                lifecycleToken,
                onError: ex =>
                {
                    _redrawPending = false;
                    _pendingInkRedrawVersionStamp = default;
                    Debug.WriteLine($"[InkRedraw] throttled-dispatch failed: {ex.GetType().Name} - {ex.Message}");
                });
            return;
        }
        _redrawPending = true;
        var directScheduled = TryBeginInvoke(() =>
        {
            var scheduledStamp = _pendingInkRedrawVersionStamp;
            _redrawPending = false;
            _pendingInkRedrawVersionStamp = default;
            if (!IsInkRedrawVersionCurrent(scheduledStamp))
            {
                RequestInkRedraw();
                return;
            }
            if (_redrawInProgress)
            {
                return;
            }
            _redrawInProgress = true;
            try
            {
                _lastInkRedrawUtc = GetCurrentUtcTimestamp();
                RedrawInkSurface();
                OnInkRedrawCompleted();
                _inkDiagnostics?.OnRedrawCompleted((GetCurrentUtcTimestamp() - _lastInkRedrawUtc).TotalMilliseconds);
            }
            finally
            {
                _redrawInProgress = false;
            }
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _redrawPending = false;
            _pendingInkRedrawVersionStamp = default;
        }
    }

    private void OnInkRedrawCompleted()
    {
        ApplyCrossPageInkVisualSync(CrossPageInkVisualSyncTrigger.InkRedrawCompleted);
    }
}
