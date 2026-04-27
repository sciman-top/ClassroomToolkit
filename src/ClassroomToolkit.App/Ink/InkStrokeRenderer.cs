using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaPen = System.Windows.Media.Pen;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Ink;

public sealed class InkStrokeRenderer
{
    private const int InkNoiseTileCacheLimit = 96;
    [SuppressMessage("Performance", "CA1802:Use literals where appropriate", Justification = "Keep the feature flag non-const so fallback branches remain compile-checked and easy to re-enable.")]
    private static readonly bool CalligraphySinglePassCompositeEnabled = true;
    private const bool CalligraphySinglePassTextureMaskEnabled = false;
    private const bool CalligraphySinglePassSealEnabled = false;
    private static readonly object InkNoiseTileCacheLock = new();
    private static readonly Dictionary<InkNoiseTileKey, InkNoiseTileEntry> InkNoiseTileCache = new();
    private static readonly LinkedList<InkNoiseTileKey> InkNoiseTileOrder = new();

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Keep instance API for compatibility with existing render-call sites.")]
    public RenderTargetBitmap RenderPage(
        InkPageData page,
        int pixelWidth,
        int pixelHeight,
        double dpiX,
        double dpiY,
        double horizontalOffsetDip = 0)
    {
        ArgumentNullException.ThrowIfNull(page);

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            var hasOffset = Math.Abs(horizontalOffsetDip) > 0.01;
            if (hasOffset)
            {
                dc.PushTransform(new TranslateTransform(horizontalOffsetDip, 0));
            }
            foreach (var stroke in page.Strokes)
            {
                RenderStroke(dc, stroke);
            }
            if (hasOffset)
            {
                dc.Pop();
            }
        }
        var bitmap = new RenderTargetBitmap(pixelWidth, pixelHeight, dpiX, dpiY, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return bitmap;
    }

    private static void RenderStroke(DrawingContext dc, InkStrokeData stroke)
    {
        var geometry = stroke.CachedGeometry;
        if (geometry == null)
        {
            geometry = InkGeometrySerializer.Deserialize(stroke.GeometryPath);
            if (geometry == null)
            {
                return;
            }
            geometry.Freeze();
            stroke.CachedGeometry = geometry;
        }
        var color = (MediaColor)MediaColorConverter.ConvertFromString(stroke.ColorHex);
        color.A = stroke.Opacity;

        if (stroke.Type == InkStrokeType.Shape || stroke.BrushStyle != PaintBrushStyle.Calligraphy)
        {
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            dc.DrawGeometry(brush, null, geometry);
            return;
        }

        var inkFlow = stroke.InkFlow;
        var strokeDirection = new Vector(stroke.StrokeDirectionX, stroke.StrokeDirectionY);
        bool inkMode = stroke.CalligraphyRenderMode == CalligraphyRenderMode.Ink;
        var suppressOverlays = stroke.Opacity < stroke.CalligraphyOverlayOpacityThreshold;
        if (CalligraphySinglePassCompositeEnabled)
        {
            var coreBrush = new SolidColorBrush(color)
            {
                Opacity = 1.0
            };
            coreBrush.Freeze();
            DrawingBrush? coreMask = null;
            if ((inkMode || CalligraphySinglePassTextureMaskEnabled) && IsInkMaskEligible(geometry, stroke.BrushSize))
            {
                coreMask = BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
            }

            if (coreMask != null)
            {
                dc.PushOpacityMask(coreMask);
                dc.DrawGeometry(coreBrush, null, geometry);
                dc.Pop();
            }
            else
            {
                dc.DrawGeometry(coreBrush, null, geometry);
            }

            if (!suppressOverlays && inkMode)
            {
                var accumulationBrush = new SolidColorBrush(color)
                {
                    Opacity = Math.Clamp(Lerp(0.04, 0.1, Math.Clamp(inkFlow, 0.0, 1.0)), 0.03, 0.11)
                };
                accumulationBrush.Freeze();
                if (coreMask != null)
                {
                    dc.PushOpacityMask(coreMask);
                    dc.DrawGeometry(accumulationBrush, null, geometry);
                    dc.Pop();
                }
                else
                {
                    dc.DrawGeometry(accumulationBrush, null, geometry);
                }
            }

            if (!suppressOverlays && inkMode && stroke.CalligraphySealEnabled && CalligraphySinglePassSealEnabled)
            {
                var sealColor = color;
                sealColor.A = (byte)Math.Clamp(Math.Round(color.A * 0.14), 0, 255);
                double sealWidth = Math.Max(stroke.BrushSize * 0.08, 0.6);
                var sealBrush = new SolidColorBrush(sealColor);
                sealBrush.Freeze();
                var sealPen = new MediaPen(sealBrush, sealWidth)
                {
                    LineJoin = PenLineJoin.Round,
                    StartLineCap = PenLineCap.Round,
                    EndLineCap = PenLineCap.Round,
                    MiterLimit = 2.4
                };
                sealPen.Freeze();
                dc.DrawGeometry(null, sealPen, geometry);
            }

            return;
        }

        if (!inkMode)
        {
            suppressOverlays = true;
        }
        if (suppressOverlays)
        {
            RenderInkCore(dc, geometry, color, stroke.BrushSize, stroke.CalligraphySealEnabled);
            RenderInkEdge(dc, geometry, color, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
            return;
        }
        if (stroke.CalligraphyInkBloomEnabled && stroke.Blooms.Count > 0)
        {
            foreach (var bloom in stroke.Blooms)
            {
                var bloomGeometry = InkGeometrySerializer.Deserialize(bloom.GeometryPath);
                if (bloomGeometry == null)
                {
                    continue;
                }
                var bloomBrush = new SolidColorBrush(color)
                {
                    Opacity = bloom.Opacity
                };
                bloomBrush.Freeze();
                dc.DrawGeometry(bloomBrush, null, bloomGeometry);
            }
        }
        RenderInkCore(dc, geometry, color, stroke.BrushSize, stroke.CalligraphySealEnabled);
        RenderInkEdge(dc, geometry, color, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
        var ribbonLayers = ResolveRibbonLayers(stroke);
        if (ribbonLayers.Count > 0)
        {
            RenderRibbonLayers(dc, geometry, ribbonLayers, color, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
        }
        else
        {
            RenderInkLayers(dc, geometry, color, inkFlow, 0.28, strokeDirection, stroke.BrushSize, stroke.MaskSeed);
        }
    }

    private static void RenderInkLayers(
        DrawingContext dc,
        Geometry geometry,
        MediaColor color,
        double inkFlow,
        double ribbonOpacity,
        Vector strokeDirection,
        double brushSize,
        int maskSeed)
    {
        var solidBrush = new SolidColorBrush(color)
        {
            Opacity = Math.Clamp(ribbonOpacity, 0.1, 1.0)
        };
        solidBrush.Freeze();
        var mask = IsInkMaskEligible(geometry, brushSize)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed)
            : null;
        if (mask == null)
        {
            dc.DrawGeometry(solidBrush, null, geometry);
            return;
        }
        dc.PushOpacityMask(mask);
        dc.DrawGeometry(solidBrush, null, geometry);
        dc.Pop();
    }

    private static IReadOnlyList<(Geometry Geometry, double Opacity)> ResolveRibbonLayers(InkStrokeData stroke)
    {
        if (stroke.Ribbons.Count == 0)
        {
            return Array.Empty<(Geometry Geometry, double Opacity)>();
        }

        if (stroke.CachedRibbonGeometries == null || stroke.CachedRibbonGeometries.Count != stroke.Ribbons.Count)
        {
            var cached = new List<Geometry>(stroke.Ribbons.Count);
            foreach (var ribbon in stroke.Ribbons)
            {
                Geometry geometry = Geometry.Empty;
                if (!string.IsNullOrWhiteSpace(ribbon.GeometryPath))
                {
                    var parsed = InkGeometrySerializer.Deserialize(ribbon.GeometryPath);
                    if (parsed != null)
                    {
                        if (parsed.CanFreeze)
                        {
                            parsed.Freeze();
                        }
                        geometry = parsed;
                    }
                }
                cached.Add(geometry);
            }
            stroke.CachedRibbonGeometries = cached;
        }

        var layers = new List<(Geometry Geometry, double Opacity)>(stroke.Ribbons.Count);
        for (int i = 0; i < stroke.Ribbons.Count; i++)
        {
            var geometry = stroke.CachedRibbonGeometries[i];
            if (geometry.Bounds.IsEmpty)
            {
                continue;
            }
            layers.Add((geometry, stroke.Ribbons[i].Opacity));
        }
        return layers;
    }

    private static void RenderRibbonLayers(
        DrawingContext dc,
        Geometry coreGeometry,
        IReadOnlyList<(Geometry Geometry, double Opacity)> ribbonLayers,
        MediaColor color,
        double inkFlow,
        Vector strokeDirection,
        double brushSize,
        int maskSeed)
    {
        var mask = IsInkMaskEligible(coreGeometry, brushSize)
            ? BuildInkOpacityMask(coreGeometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed)
            : null;

        foreach (var ribbon in ribbonLayers)
        {
            var ribbonBrush = new SolidColorBrush(color)
            {
                Opacity = Math.Clamp(ribbon.Opacity * 0.35, 0.06, 0.45)
            };
            ribbonBrush.Freeze();
            if (mask == null)
            {
                dc.DrawGeometry(ribbonBrush, null, ribbon.Geometry);
                continue;
            }
            dc.PushOpacityMask(mask);
            dc.DrawGeometry(ribbonBrush, null, ribbon.Geometry);
            dc.Pop();
        }
    }

    private static void RenderInkCore(DrawingContext dc, Geometry geometry, MediaColor color, double brushSize, bool enableSeal)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        dc.DrawGeometry(brush, null, geometry);
        if (!enableSeal)
        {
            return;
        }
        double sealWidth = Math.Max(brushSize * 0.08, 0.6);
        if (sealWidth <= 0)
        {
            return;
        }
        var pen = new MediaPen(brush, sealWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            MiterLimit = 2.4
        };
        pen.Freeze();
        dc.DrawGeometry(null, pen, geometry);
    }

    private static void RenderInkEdge(
        DrawingContext dc,
        Geometry geometry,
        MediaColor color,
        double inkFlow,
        Vector strokeDirection,
        double brushSize,
        int maskSeed)
    {
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double edgeOpacity = Math.Clamp(Lerp(0.14, 0.3, dryFactor), 0.08, 0.45);
        double edgeWidth = Math.Max(brushSize * Lerp(0.04, 0.09, dryFactor), 0.55);
        var edgeBrush = new SolidColorBrush(color)
        {
            Opacity = edgeOpacity
        };
        edgeBrush.Freeze();
        var pen = new MediaPen(edgeBrush, edgeWidth)
        {
            LineJoin = PenLineJoin.Round,
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            MiterLimit = 2.4
        };
        pen.Freeze();
        var mask = IsInkMaskEligible(geometry, brushSize)
            ? BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, brushSize, maskSeed)
            : null;
        if (mask == null)
        {
            dc.DrawGeometry(null, pen, geometry);
            return;
        }
        dc.PushOpacityMask(mask);
        dc.DrawGeometry(null, pen, geometry);
        dc.Pop();
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
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

    private static DrawingBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector strokeDirection, double brushSize, int seed)
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

    private static void ApplyInkTextureTransform(ImageBrush brush, Rect bounds, Vector strokeDirection, double dryFactor)
    {
        var dir = strokeDirection;
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

    private static WriteableBitmap CreateInkNoiseTileCore(int size, double baseAlpha, double variation, int seed)
    {
        var rng = new Random(seed);
        int grid = 14;
        var gridValues = new double[grid + 1][];
        for (int x = 0; x <= grid; x++)
        {
            gridValues[x] = new double[grid + 1];
        }

        for (int y = 0; y <= grid; y++)
        {
            for (int x = 0; x <= grid; x++)
            {
                double jitter = (rng.NextDouble() * 2.0 - 1.0) * variation;
                gridValues[x][y] = Math.Clamp(baseAlpha + jitter, 0.0, 1.0);
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

                double n0 = Lerp(gridValues[x0][y0], gridValues[x1][y0], tx);
                double n1 = Lerp(gridValues[x0][y1], gridValues[x1][y1], tx);
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
}
