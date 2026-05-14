using System;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
}
