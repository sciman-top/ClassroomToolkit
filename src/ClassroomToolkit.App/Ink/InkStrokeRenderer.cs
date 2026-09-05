using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Ink;

internal sealed class InkStrokeRenderer
{
    private static readonly MediaColor DefaultStrokeColor = MediaColor.FromRgb(255, 0, 0);
    private readonly Dictionary<string, MediaColor> _strokeColorCache = new(StringComparer.Ordinal);
    private readonly Dictionary<int, SolidColorBrush> _solidBrushCache = new();
    private readonly InkOpacityMaskCache _opacityMaskCache = new(
        InkRenderingCacheDefaults.OpacityMaskCacheLimit);
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
        InkPayloadNormalizer.NormalizePage(page);

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

    private MediaColor ResolveStrokeColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return DefaultStrokeColor;
        }

        if (_strokeColorCache.TryGetValue(colorHex, out var cached))
        {
            return cached;
        }

        MediaColor color;
        try
        {
            if (MediaColorConverter.ConvertFromString(colorHex) is not MediaColor parsed)
            {
                return DefaultStrokeColor;
            }

            color = parsed;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(ex))
        {
            return DefaultStrokeColor;
        }

        if (_strokeColorCache.Count >= InkRenderingCacheDefaults.StrokeColorCacheLimit)
        {
            _strokeColorCache.Clear();
        }
        _strokeColorCache[colorHex] = color;
        return color;
    }

    private SolidColorBrush GetCachedSolidBrush(MediaColor color)
    {
        int key = PackColorKey(color);
        if (_solidBrushCache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        if (_solidBrushCache.Count >= InkCacheRuntimeDefaults.SolidBrushCacheLimit)
        {
            _solidBrushCache.Clear();
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        _solidBrushCache[key] = brush;
        return brush;
    }

    private static int PackColorKey(MediaColor color)
    {
        return (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;
    }

    private void RenderStroke(DrawingContext dc, InkStrokeData stroke)
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
        var color = ResolveStrokeColor(stroke.ColorHex);
        color.A = stroke.Opacity;

        if (stroke.Type == InkStrokeType.Shape || stroke.BrushStyle != PaintBrushStyle.Calligraphy)
        {
            var brush = GetCachedSolidBrush(color);
            dc.DrawGeometry(brush, null, geometry);
            return;
        }

        var inkFlow = stroke.InkFlow;
        var strokeDirection = new Vector(stroke.StrokeDirectionX, stroke.StrokeDirectionY);
        bool inkMode = stroke.CalligraphyRenderMode == CalligraphyRenderMode.Ink;
        var suppressOverlays = stroke.Opacity < stroke.CalligraphyOverlayOpacityThreshold;
        var coreBrush = GetCachedSolidBrush(color);
        DrawingBrush? coreMask = null;
        if (inkMode && IsInkMaskEligible(geometry, stroke.BrushSize))
        {
            coreMask = _opacityMaskCache.GetOrCreate(
                geometry.Bounds,
                inkFlow,
                strokeDirection,
                stroke.BrushSize,
                stroke.MaskSeed,
                InkOpacityMaskCache.ExportTextureVariant,
                () => BuildInkOpacityMask(geometry.Bounds, inkFlow, strokeDirection, stroke.BrushSize, stroke.MaskSeed));
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
        var tile = InkNoiseTileCache.GetOrCreate(tileSize, baseAlpha, variation, effectiveSeed);

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

}
