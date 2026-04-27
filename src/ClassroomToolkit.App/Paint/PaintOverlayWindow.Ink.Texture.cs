using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MediaBrush = System.Windows.Media.Brush;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private const int InkNoiseSeedVariants = 64;

    private MediaBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector? strokeDirection)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }

        int tileSize = (int)Math.Round(Math.Clamp(_brushSize * 2.2, 18, 90));
        int detailTileSize = (int)Math.Round(Math.Clamp(tileSize * 0.62, 12, 56));
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double baseAlpha = Lerp(0.74, 0.93, inkFlow);
        double variation = Lerp(0.1, 0.18, dryFactor);
        double detailVariation = Lerp(0.05, 0.11, dryFactor * 0.7);
        int anchorX = (int)Math.Round(bounds.X * 0.35);
        int anchorY = (int)Math.Round(bounds.Y * 0.35);
        int seedBucket = _inkRandom.Next(InkNoiseSeedVariants);
        int effectiveSeed = HashCode.Combine(tileSize, seedBucket, anchorX, anchorY);
        int detailSeed = HashCode.Combine(detailTileSize, seedBucket + 97, anchorY, anchorX);
        var tile = CreateInkNoiseTile(tileSize, baseAlpha, variation, effectiveSeed);
        var detailTile = CreateInkNoiseTile(detailTileSize, baseAlpha, detailVariation, detailSeed);

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.58 + (inkFlow * 0.22), 0.48, 0.92)
        };
        ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 0, translationJitterDip: 0);
        texture.Freeze();

        var detailTexture = new ImageBrush(detailTile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X + detailTileSize * 0.3, bounds.Y + detailTileSize * 0.2, detailTileSize, detailTileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.18 + (dryFactor * 0.14), 0.12, 0.36)
        };
        double detailJitter = (seedBucket % 7) - 3;
        ApplyInkTextureTransform(detailTexture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 90, translationJitterDip: detailJitter);
        detailTexture.Freeze();

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
        radial.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();

        var maskRect = new RectangleGeometry(bounds);
        if (maskRect.CanFreeze)
        {
            maskRect.Freeze();
        }
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(System.Windows.Media.Brushes.White, null, maskRect));
        group.Children.Add(new GeometryDrawing(radial, null, maskRect));
        group.Children.Add(new GeometryDrawing(texture, null, maskRect));
        group.Children.Add(new GeometryDrawing(detailTexture, null, maskRect));
        group.Freeze();
        var mask = new DrawingBrush(group) { Stretch = Stretch.None };
        if (mask.CanFreeze)
        {
            mask.Freeze();
        }
        return mask;
    }

    private static MediaBrush? BuildInkOpacityMask(Rect bounds, double inkFlow, Vector? strokeDirection, double brushSize, int seed)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }
        int tileSize = (int)Math.Round(Math.Clamp(brushSize * 2.2, 18, 90));
        int detailTileSize = (int)Math.Round(Math.Clamp(tileSize * 0.62, 12, 56));
        double dryFactor = Math.Clamp(1.0 - inkFlow, 0, 1);
        double baseAlpha = Lerp(0.74, 0.93, inkFlow);
        double variation = Lerp(0.1, 0.18, dryFactor);
        double detailVariation = Lerp(0.05, 0.11, dryFactor * 0.7);
        int safeSeed = seed == 0 ? 17 : seed;
        int anchorX = (int)Math.Round(bounds.X * 0.35);
        int anchorY = (int)Math.Round(bounds.Y * 0.35);
        int effectiveSeed = HashCode.Combine(safeSeed, tileSize, anchorX, anchorY);
        int detailSeed = HashCode.Combine(safeSeed, detailTileSize, anchorY, anchorX, 97);
        var tile = CreateInkNoiseTile(tileSize, baseAlpha, variation, effectiveSeed);
        var detailTile = CreateInkNoiseTile(detailTileSize, baseAlpha, detailVariation, detailSeed);

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.58 + (inkFlow * 0.22), 0.48, 0.92)
        };
        ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 0, translationJitterDip: 0);
        texture.Freeze();

        var detailTexture = new ImageBrush(detailTile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X + detailTileSize * 0.3, bounds.Y + detailTileSize * 0.2, detailTileSize, detailTileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.18 + (dryFactor * 0.14), 0.12, 0.36)
        };
        double detailJitter = (Math.Abs(safeSeed) % 7) - 3;
        ApplyInkTextureTransform(detailTexture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 90, translationJitterDip: detailJitter);
        detailTexture.Freeze();

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
        radial.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(System.Windows.Media.Color.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();

        var maskRect = new RectangleGeometry(bounds);
        if (maskRect.CanFreeze)
        {
            maskRect.Freeze();
        }
        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(System.Windows.Media.Brushes.White, null, maskRect));
        group.Children.Add(new GeometryDrawing(radial, null, maskRect));
        group.Children.Add(new GeometryDrawing(texture, null, maskRect));
        group.Children.Add(new GeometryDrawing(detailTexture, null, maskRect));
        group.Freeze();
        var mask = new DrawingBrush(group) { Stretch = Stretch.None };
        if (mask.CanFreeze)
        {
            mask.Freeze();
        }
        return mask;
    }

    private static void ApplyInkTextureTransform(
        ImageBrush brush,
        Rect bounds,
        Vector? strokeDirection,
        double dryFactor,
        double angleOffsetDegrees,
        double translationJitterDip)
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

        double angle = Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI + angleOffsetDegrees;
        double centerX = bounds.X + bounds.Width * 0.5;
        double centerY = bounds.Y + bounds.Height * 0.5;
        double stretch = Lerp(1.3, 1.8, dryFactor);
        double squash = Lerp(0.85, 0.6, dryFactor);

        var transforms = new TransformGroup();
        transforms.Children.Add(new ScaleTransform(stretch, squash, centerX, centerY));
        transforms.Children.Add(new RotateTransform(angle, centerX, centerY));
        if (Math.Abs(translationJitterDip) > 0.01)
        {
            transforms.Children.Add(new TranslateTransform(translationJitterDip, -translationJitterDip * 0.7));
        }
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
