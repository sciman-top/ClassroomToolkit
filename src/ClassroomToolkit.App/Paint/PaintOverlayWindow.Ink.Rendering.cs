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
        
        var renderGeometry = _photoModeActive ? ToScreenGeometry(geometry) : geometry;
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
}
