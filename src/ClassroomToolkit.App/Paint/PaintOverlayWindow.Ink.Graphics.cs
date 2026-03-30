using System;
using System.Collections.Generic;
using System.Buffers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaColor = System.Windows.Media.Color;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;
using MediaBrushes = System.Windows.Media.Brushes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool ShouldRenderInteractiveInkInPhotoSpace()
    {
        return PhotoInkRenderPolicy.ShouldRenderInteractiveInkInPhotoSpace(
            IsPhotoInkModeActive(),
            RasterImage.RenderTransform,
            _photoContentTransform);
    }

    private Geometry NormalizeInteractiveInkGeometry(Geometry geometry)
    {
        if (ShouldRenderInteractiveInkInPhotoSpace())
        {
            return ToPhotoGeometry(geometry) ?? geometry;
        }

        if (PhotoInkPanCompensationGeometryPolicy.ShouldApplyCompensation(
                IsPhotoInkModeActive(),
                RasterImage.RenderTransform,
                _photoInkPanCompensation))
        {
            return PhotoInkPanCompensationGeometryPolicy.AdjustToRasterSpace(
                geometry,
                _photoInkPanCompensation.X,
                _photoInkPanCompensation.Y);
        }

        return geometry;
    }

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

    private void CommitGeometryFill(Geometry geometry, MediaColor color)
    {
        var brush = GetCachedSolidBrush(color);
        var renderGeometry = NormalizeInteractiveInkGeometry(geometry);
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
                    var strokeGeometry = NormalizeInteractiveInkGeometry(coreGeometry);
                    List<(Geometry Geometry, double Opacity)>? ribbonLayers = null;
                    IEnumerable<(Geometry Geometry, double Opacity)>? blooms = null;
                    if (!CalligraphySinglePassCompositeEnabled)
                    {
                        var ribbons = calligraphyRenderer.GetLastRibbonGeometries();
                        if (ribbons != null && ribbons.Count > 0)
                        {
                            ribbonLayers = new List<(Geometry Geometry, double Opacity)>(ribbons.Count);
                            foreach (var ribbon in ribbons)
                            {
                                var ribbonGeometry = NormalizeInteractiveInkGeometry(ribbon.Geometry);
                                if (ribbonGeometry == null || ribbonGeometry.Bounds.IsEmpty)
                                {
                                    continue;
                                }
                                ribbonLayers.Add((
                                    ribbonGeometry,
                                    calligraphyRenderer.GetRibbonOpacity(ribbon.RibbonT)));
                            }
                        }
                        blooms = _calligraphyInkBloomEnabled
                            ? calligraphyRenderer.GetInkBloomGeometries()
                                ?.Select(bloom => (NormalizeInteractiveInkGeometry(bloom.Geometry), bloom.Opacity))
                            : null;
                    }
                    RenderCalligraphyComposite(
                        strokeGeometry,
                        color,
                        _brushSize,
                        inkFlow,
                        strokeDirection,
                        _calligraphyRenderMode,
                        _calligraphySealEnabled,
                        _calligraphyInkBloomEnabled,
                        ribbonLayers,
                        blooms,
                        suppressOverlays,
                        maskSeed: null);
                    return;
                }
            }
        }
        if (isCalligraphy)
        {
            RenderInkLayers(renderGeometry, color, inkFlow, 1.0, strokeDirection);
            return;
        }
        RenderAndBlend(renderGeometry, brush, null, erase: false, null);
    }

    private void CommitGeometryStroke(Geometry geometry, MediaPen pen)
    {
        var renderGeometry = NormalizeInteractiveInkGeometry(geometry);
        RenderAndBlend(renderGeometry, null, pen, erase: false, null);
    }

    private bool EraseGeometry(Geometry geometry)
    {
        var changed = ApplyInkErase(geometry);
        if (!changed && _inkRecordEnabled)
        {
            return false;
        }
        var renderGeometry = NormalizeInteractiveInkGeometry(geometry);
        RenderAndBlend(renderGeometry, MediaBrushes.White, null, erase: true, null);
        return changed || !_inkRecordEnabled;
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
        var dpi = VisualTreeHelper.GetDpi(this);
        var surfaceRect = new Int32Rect(0, 0, _surfacePixelWidth, _surfacePixelHeight);
        var batches = BuildDrawCommandBatches(commands, dpi, surfaceRect);
        if (batches.Count == 0)
        {
            return;
        }

        bool blended = false;
        foreach (var batch in batches)
        {
            if (!TryRenderGeometryBatch(batch.Commands, batch.DestRect, dpi, out var rect, out var pixels, out var stride, out var bufferLength))
            {
                continue;
            }
            try
            {
                BlendSourceOver(rect, pixels, stride);
                blended = true;
            }
            finally
            {
                PixelPool.Return(pixels, clearArray: false);
            }
        }

        if (blended)
        {
            _hasDrawing = true;
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
        using (var dc = _scratchRenderVisual.RenderOpen())
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
        return TryRenderScratchVisual(destRect, out pixels, out stride, out bufferLength);
    }

    private readonly struct DrawCommandBatch
    {
        public DrawCommandBatch(Int32Rect destRect, List<DrawCommand> commands)
        {
            DestRect = destRect;
            Commands = commands;
        }

        public Int32Rect DestRect { get; }
        public List<DrawCommand> Commands { get; }
    }

    private static List<DrawCommandBatch> BuildDrawCommandBatches(
        IReadOnlyList<DrawCommand> commands,
        DpiScale dpi,
        Int32Rect surfaceRect)
    {
        if (commands.Count > 0 && commands.Count <= 24 && TryBuildSingleBatch(commands, dpi, surfaceRect, out var single))
        {
            return single;
        }

        var batches = new List<DrawCommandBatch>();
        List<DrawCommand>? currentCommands = null;
        var currentRect = new Int32Rect(0, 0, 0, 0);

        foreach (var command in commands)
        {
            if (!TryGetCommandRenderRect(command, dpi, surfaceRect, out var commandRect))
            {
                continue;
            }

            if (currentCommands == null)
            {
                currentCommands = new List<DrawCommand>(capacity: 4) { command };
                currentRect = commandRect;
                continue;
            }

            var unionRect = UnionRects(currentRect, commandRect);
            if (ShouldSplitRenderBatch(currentRect, commandRect, unionRect))
            {
                batches.Add(new DrawCommandBatch(currentRect, currentCommands));
                currentCommands = new List<DrawCommand>(capacity: 4) { command };
                currentRect = commandRect;
            }
            else
            {
                currentCommands.Add(command);
                currentRect = unionRect;
            }
        }

        if (currentCommands != null && currentCommands.Count > 0)
        {
            batches.Add(new DrawCommandBatch(currentRect, currentCommands));
        }
        return batches;
    }

    private static bool TryBuildSingleBatch(
        IReadOnlyList<DrawCommand> commands,
        DpiScale dpi,
        Int32Rect surfaceRect,
        out List<DrawCommandBatch> batches)
    {
        batches = new List<DrawCommandBatch>();
        var mergedCommands = new List<DrawCommand>(commands.Count);
        var mergedRect = new Int32Rect(0, 0, 0, 0);
        var hasRect = false;

        foreach (var command in commands)
        {
            if (!TryGetCommandRenderRect(command, dpi, surfaceRect, out var rect))
            {
                continue;
            }

            mergedCommands.Add(command);
            mergedRect = hasRect ? UnionRects(mergedRect, rect) : rect;
            hasRect = true;
        }

        if (!hasRect || mergedCommands.Count == 0)
        {
            return false;
        }

        double commandArea = 0;
        foreach (var command in mergedCommands)
        {
            if (TryGetCommandRenderRect(command, dpi, surfaceRect, out var rect))
            {
                commandArea += RectArea(rect);
            }
        }

        double mergedArea = RectArea(mergedRect);
        if (commandArea <= 0 || mergedArea <= 0)
        {
            return false;
        }

        // Keep one-pass rendering when union rect is reasonably compact.
        if (mergedArea > commandArea * 2.4)
        {
            return false;
        }

        batches.Add(new DrawCommandBatch(mergedRect, mergedCommands));
        return true;
    }

    private static bool TryGetCommandRenderRect(
        DrawCommand command,
        DpiScale dpi,
        Int32Rect surfaceRect,
        out Int32Rect commandRect)
    {
        commandRect = new Int32Rect(0, 0, 0, 0);
        var geometry = command.Geometry;
        if (geometry == null || geometry.Bounds.IsEmpty)
        {
            return false;
        }

        var bounds = command.Pen != null ? geometry.GetRenderBounds(command.Pen) : geometry.Bounds;
        if (bounds.IsEmpty)
        {
            return false;
        }

        bounds.Inflate(2, 2);
        var rawRect = new Int32Rect(
            (int)Math.Floor(bounds.X * dpi.DpiScaleX),
            (int)Math.Floor(bounds.Y * dpi.DpiScaleY),
            (int)Math.Ceiling(bounds.Width * dpi.DpiScaleX),
            (int)Math.Ceiling(bounds.Height * dpi.DpiScaleY));
        commandRect = IntersectRects(rawRect, surfaceRect);
        return commandRect.Width > 0 && commandRect.Height > 0;
    }

    private static bool ShouldSplitRenderBatch(Int32Rect currentRect, Int32Rect nextRect, Int32Rect unionRect)
    {
        const int proximityPaddingPixels = InkRenderBatchingDefaults.ProximityPaddingPixels;
        const double areaRatioThreshold = InkRenderBatchingDefaults.AreaRatioThreshold;

        if (RectsNear(currentRect, nextRect, proximityPaddingPixels))
        {
            return false;
        }

        double currentArea = RectArea(currentRect);
        double nextArea = RectArea(nextRect);
        double unionArea = RectArea(unionRect);
        if (currentArea <= 0 || nextArea <= 0 || unionArea <= 0)
        {
            return false;
        }

        double packedArea = currentArea + nextArea;
        if (unionArea <= packedArea)
        {
            return false;
        }

        return unionArea > packedArea * areaRatioThreshold;
    }

    private static bool RectsNear(Int32Rect a, Int32Rect b, int padding)
    {
        long leftA = (long)a.X - padding;
        long topA = (long)a.Y - padding;
        long rightA = (long)a.X + a.Width + padding;
        long bottomA = (long)a.Y + a.Height + padding;
        long leftB = b.X;
        long topB = b.Y;
        long rightB = (long)b.X + b.Width;
        long bottomB = (long)b.Y + b.Height;
        return leftA < rightB && leftB < rightA && topA < bottomB && topB < bottomA;
    }

    private static double RectArea(Int32Rect rect)
    {
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return 0;
        }
        return (double)rect.Width * rect.Height;
    }

    private bool TryRenderGeometryBatch(
        IReadOnlyList<DrawCommand> commands,
        Int32Rect destRect,
        DpiScale dpi,
        out Int32Rect renderedRect,
        out byte[] pixels,
        out int stride,
        out int bufferLength)
    {
        renderedRect = new Int32Rect(0, 0, 0, 0);
        pixels = Array.Empty<byte>();
        stride = 0;
        bufferLength = 0;
        if (_rasterSurface == null || destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }
        renderedRect = destRect;

        using (var dc = _scratchRenderVisual.RenderOpen())
        {
            var offsetX = renderedRect.X / dpi.DpiScaleX;
            var offsetY = renderedRect.Y / dpi.DpiScaleY;
            dc.PushTransform(new TranslateTransform(-offsetX, -offsetY));
            foreach (var command in commands)
            {
                var geometry = command.Geometry;
                if (geometry == null || geometry.Bounds.IsEmpty)
                {
                    continue;
                }
                if (command.ClipGeometry != null)
                {
                    dc.PushClip(command.ClipGeometry);
                }
                if (command.OpacityMask != null)
                {
                    dc.PushOpacityMask(command.OpacityMask);
                }
                dc.DrawGeometry(command.Fill, command.Pen, geometry);
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
        return TryRenderScratchVisual(renderedRect, out pixels, out stride, out bufferLength);
    }

    private bool TryRenderScratchVisual(
        Int32Rect destRect,
        out byte[] pixels,
        out int stride,
        out int bufferLength)
    {
        pixels = Array.Empty<byte>();
        stride = 0;
        bufferLength = 0;

        if (destRect.Width <= 0 || destRect.Height <= 0)
        {
            return false;
        }

        stride = destRect.Width * 4;
        bufferLength = stride * destRect.Height;
        if (bufferLength <= 0)
        {
            return false;
        }

        var rtb = new RenderTargetBitmap(destRect.Width, destRect.Height, _surfaceDpiX, _surfaceDpiY, PixelFormats.Pbgra32);
        rtb.Render(_scratchRenderVisual);
        pixels = PixelPool.Rent(bufferLength);
        rtb.CopyPixels(pixels, stride, 0);
        return true;
    }

    private void ApplyEraseMask(Int32Rect rect, byte[] maskPixels, int maskStride)
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
                int d = destRow + x * 4;
                if (maskA == byte.MaxValue)
                {
                    destPixels[d] = 0;
                    destPixels[d + 1] = 0;
                    destPixels[d + 2] = 0;
                    destPixels[d + 3] = 0;
                    continue;
                }

                int invA = 255 - maskA;
                destPixels[d] = (byte)(destPixels[d] * invA / 255);
                destPixels[d + 1] = (byte)(destPixels[d + 1] * invA / 255);
                destPixels[d + 2] = (byte)(destPixels[d + 2] * invA / 255);
                destPixels[d + 3] = (byte)(destPixels[d + 3] * invA / 255);
            }
        }
        _rasterSurface.WritePixels(rect, destPixels, destStride, 0);
    }
}
