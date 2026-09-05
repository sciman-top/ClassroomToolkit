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

    private void RenderStoredStroke(InkStrokeData stroke, List<DrawCommand> simpleStrokeBatch)
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
        var renderGeometry = ResolveStoredInkRenderGeometry(geometry, photoInkModeActive, usePhotoTransform);
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
        if (!TryGetCachedStrokeColor(stroke.ColorHex, out var color))
        {
            color = Colors.Red;
        }
        color.A = stroke.Opacity;
        if (stroke.Type == InkStrokeType.Shape || stroke.BrushStyle != PaintBrushStyle.Calligraphy)
        {
            var brush = GetCachedSolidBrush(color);
            simpleStrokeBatch.Add(new DrawCommand(renderGeometry, brush, null, null, null));
            if (simpleStrokeBatch.Count >= 24)
            {
                RenderAndBlendBatch(simpleStrokeBatch);
                simpleStrokeBatch.Clear();
            }
            return;
        }
        if (simpleStrokeBatch.Count > 0)
        {
            RenderAndBlendBatch(simpleStrokeBatch);
            simpleStrokeBatch.Clear();
        }
        var inkFlow = stroke.InkFlow;
        var strokeDirection = new Vector(stroke.StrokeDirectionX, stroke.StrokeDirectionY);
        bool suppressOverlays = stroke.Opacity < stroke.CalligraphyOverlayOpacityThreshold;
        RenderCalligraphyComposite(
            renderGeometry,
            color,
            stroke.BrushSize,
            inkFlow,
            strokeDirection,
            stroke.CalligraphyRenderMode,
            suppressOverlays,
            stroke.MaskSeed);
    }

    private Geometry? ResolveStoredInkRenderGeometry(
        Geometry geometry,
        bool photoInkModeActive,
        bool usePhotoTransform)
    {
        if (usePhotoTransform)
        {
            return geometry;
        }

        return photoInkModeActive ? ToScreenGeometry(geometry) : geometry;
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

    private void RenderCalligraphyComposite(
        Geometry geometry,
        MediaColor color,
        double brushSize,
        double inkFlow,
        Vector? strokeDirection,
        CalligraphyRenderMode renderMode,
        bool suppressOverlays,
        int? maskSeed)
    {
        bool inkMode = renderMode == CalligraphyRenderMode.Ink;
        bool overlaysEnabled = !suppressOverlays && inkMode;
        int seededMaskValue = maskSeed ?? ResolveDeterministicMaskSeed(geometry, color, brushSize, renderMode);
        bool maskEligible = inkMode && IsInkMaskEligible(geometry, brushSize);
        MediaBrush? coreMask = maskEligible
            ? GetCachedInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, seededMaskValue)
            : null;
        var commands = new List<DrawCommand>(overlaysEnabled ? 2 : 1)
        {
            new(geometry, GetCachedSolidBrush(color, opacity: 1.0), null, coreMask, null)
        };
        if (overlaysEnabled)
        {
            double accumulationOpacity = Math.Clamp(Lerp(0.04, 0.1, Math.Clamp(inkFlow, 0.0, 1.0)), 0.03, 0.11);
            commands.Add(new DrawCommand(
                geometry,
                GetCachedSolidBrush(color, opacity: accumulationOpacity),
                null,
                coreMask,
                null));
        }
        RenderAndBlendBatch(commands);
    }

    private bool ShouldSuppressCalligraphyOverlays()
    {
        // In photo/PDF mode prioritize stroke stability and latency over decorative overlays.
        return IsPhotoInkModeActive() || _brushOpacity < _calligraphyOverlayOpacityThreshold;
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

        var simpleStrokeBatch = new List<DrawCommand>(Math.Min(_inkStrokes.Count, 24));
        foreach (var stroke in _inkStrokes)
        {
            RenderStoredStroke(stroke, simpleStrokeBatch);
        }
        if (simpleStrokeBatch.Count > 0)
        {
            RenderAndBlendBatch(simpleStrokeBatch);
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
        var throttleActive = IsPhotoInkModeActive()
            && (IsCrossPagePanOrDragActive() || _photoWheelZoomAnimationActive || IsPhotoZoomInteractionActive());
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
                        RunPendingInkRedraw();
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
            RunPendingInkRedraw();
        }, DispatcherPriority.Render);
        if (!directScheduled)
        {
            _redrawPending = false;
            _pendingInkRedrawVersionStamp = default;
        }
    }

    private void RunPendingInkRedraw()
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
    }

    private void OnInkRedrawCompleted()
    {
        ApplyCrossPageInkVisualSync(CrossPageInkVisualSyncTrigger.InkRedrawCompleted);
    }
}
