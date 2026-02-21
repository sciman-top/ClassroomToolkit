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
            _photoModeActive,
            RasterImage.RenderTransform,
            _photoContentTransform);
    }

    private Geometry NormalizeInteractiveInkGeometry(Geometry geometry)
    {
        if (!ShouldRenderInteractiveInkInPhotoSpace())
        {
            return geometry;
        }

        return ToPhotoGeometry(geometry) ?? geometry;
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
        var brush = new SolidColorBrush(color);
        brush.Freeze();
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
                    strokeGeometry = NormalizeInteractiveInkGeometry(strokeGeometry);
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
                        blooms?.Select(bloom => (NormalizeInteractiveInkGeometry(bloom.Geometry), bloom.Opacity)),
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

    private void EraseGeometry(Geometry geometry)
    {
        var changed = ApplyInkErase(geometry);
        if (!changed && _inkRecordEnabled)
        {
            return;
        }
        var renderGeometry = NormalizeInteractiveInkGeometry(geometry);
        RenderAndBlend(renderGeometry, MediaBrushes.White, null, erase: true, null);
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
}
