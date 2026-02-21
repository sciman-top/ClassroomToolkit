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

        if (!_photoModeActive && stroke.CachedBounds.HasValue)
        {
             var bounds = stroke.CachedBounds.Value;
             if (bounds.Right < 0 || bounds.Bottom < 0 || bounds.Left > _surfacePixelWidth || bounds.Top > _surfacePixelHeight)
             {
                 return;
             }
        }
        
        var usePhotoTransform = _photoModeActive && ReferenceEquals(RasterImage.RenderTransform, _photoContentTransform);
        var renderGeometry = usePhotoTransform
            ? geometry
            : (_photoModeActive ? ToScreenGeometry(geometry) : geometry);
        if (renderGeometry == null)
        {
            return;
        }
        
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
                var renderBloom = usePhotoTransform
                    ? bloomGeometry
                    : (_photoModeActive ? ToScreenGeometry(bloomGeometry) : bloomGeometry);
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
        ClearSurface();
        foreach (var stroke in _inkStrokes)
        {
            RenderStoredStroke(stroke);
        }
        _hasDrawing = _inkStrokes.Count > 0;
        _perfRedrawSurface.Add(redrawSw.Elapsed.TotalMilliseconds, Dispatcher.CheckAccess());
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("redraw-exit", $"ms={redrawSw.Elapsed.TotalMilliseconds:F2}");
        }
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
        _inkDiagnostics?.OnRedrawRequested(throttleActive && elapsedMs < InkRedrawMinIntervalMs);
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
                        _inkDiagnostics?.OnRedrawCompleted((DateTime.UtcNow - _lastInkRedrawUtc).TotalMilliseconds);
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
                _inkDiagnostics?.OnRedrawCompleted((DateTime.UtcNow - _lastInkRedrawUtc).TotalMilliseconds);
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
}
