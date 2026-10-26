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
using MediaBrush = System.Windows.Media.Brush;
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
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
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

        var inkFlow = ResolveInkFlow(stroke.InkFlow);
        var strokeDirection = new Vector(stroke.StrokeDirectionX, stroke.StrokeDirectionY);
        bool inkMode = stroke.CalligraphyRenderMode == CalligraphyRenderMode.Ink;
        var suppressOverlays = stroke.Opacity < stroke.CalligraphyOverlayOpacityThreshold;
        var coreBrush = GetCachedSolidBrush(color);
        DrawingBrush? coreMask = null;
        if (inkMode && InkMaskPainters.IsInkMaskEligible(geometry, stroke.BrushSize))
        {
            coreMask = _opacityMaskCache.GetOrCreate(
                geometry.Bounds,
                inkFlow,
                strokeDirection,
                stroke.BrushSize,
                stroke.MaskSeed,
                stroke.WetnessStart,
                stroke.WetnessEnd,
                InkOpacityMaskCache.ExportTextureVariant,
                () => BuildInkOpacityMask(
                    geometry.Bounds,
                    inkFlow,
                    strokeDirection,
                    stroke.BrushSize,
                    stroke.MaskSeed,
                    stroke.WetnessStart,
                    stroke.WetnessEnd));
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
            if (stroke.CalligraphyInkBloomEnabled)
            {
                var accumulationBrush = new SolidColorBrush(color)
                {
                    Opacity = Math.Clamp(Lerp(0.04, 0.1, Math.Clamp(inkFlow, 0.0, 1.0)), 0.03, 0.11)
                };
                accumulationBrush.Freeze();
                DrawMaskedGeometry(dc, accumulationBrush, geometry, coreMask);
            }

            if (stroke.CalligraphySealEnabled)
            {
                var sealBrush = CreateOverlayBrush(color, 0.035);
                DrawMaskedGeometry(dc, sealBrush, geometry, coreMask);
            }

            // Ribbon/bloom geometry is persisted separately so overlapping source-over
            // layers can actually deepen the stroke instead of collapsing to a union.
            if (stroke.CalligraphySealEnabled)
            {
                foreach (var ribbon in stroke.Ribbons)
                {
                    var ribbonGeometry = InkGeometrySerializer.Deserialize(ribbon.GeometryPath);
                    if (ribbonGeometry == null || ribbonGeometry.Bounds.IsEmpty)
                    {
                        continue;
                    }

                    var ribbonBrush = CreateOverlayBrush(color, ribbon.Opacity);
                    DrawMaskedGeometry(dc, ribbonBrush, ribbonGeometry, coreMask);
                }
            }

            if (stroke.CalligraphyInkBloomEnabled)
            {
                foreach (var bloom in stroke.Blooms)
                {
                    var bloomGeometry = InkGeometrySerializer.Deserialize(bloom.GeometryPath);
                    if (bloomGeometry == null || bloomGeometry.Bounds.IsEmpty)
                    {
                        continue;
                    }

                    var bloomBrush = CreateOverlayBrush(color, bloom.Opacity);
                    DrawMaskedGeometry(dc, bloomBrush, bloomGeometry, coreMask);
                }
            }
        }
    }

    private static SolidColorBrush CreateOverlayBrush(MediaColor color, double opacity)
    {
        var brush = new SolidColorBrush(color)
        {
            Opacity = Math.Clamp(opacity, 0.0, 1.0)
        };
        brush.Freeze();
        return brush;
    }

    private static void DrawMaskedGeometry(
        DrawingContext dc,
        MediaBrush brush,
        Geometry geometry,
        DrawingBrush? mask)
    {
        if (mask != null)
        {
            dc.PushOpacityMask(mask);
            dc.DrawGeometry(brush, null, geometry);
            dc.Pop();
        }
        else
        {
            dc.DrawGeometry(brush, null, geometry);
        }
    }

    private static double Lerp(double a, double b, double t)
    {
        return a + (b - a) * t;
    }

    /// <summary>
    /// 单笔有效干燥度：InkFlow 基线 + 起收湿感差分（越写越干的笔画获得更强纹理变化）。
    /// 湿感字段为附加式可选；旧数据（null）保持旧行为。
    /// </summary>
    internal static double ResolveInkDryFactor(double inkFlow, double? wetnessStart, double? wetnessEnd)
    {
        double safeInkFlow = ResolveInkFlow(inkFlow);
        double baseDry = 1.0 - safeInkFlow;
        if (!wetnessStart.HasValue
            || !wetnessEnd.HasValue
            || !double.IsFinite(wetnessStart.Value)
            || !double.IsFinite(wetnessEnd.Value))
        {
            return baseDry;
        }

        double wetnessDrop = Math.Clamp(
            Math.Clamp(wetnessStart.Value, 0.0, 1.0)
            - Math.Clamp(wetnessEnd.Value, 0.0, 1.0),
            0.0,
            1.0);
        return Math.Clamp(baseDry + (wetnessDrop * 0.35), 0.0, 1.0);
    }

    internal static double ResolveInkFlow(double inkFlow)
    {
        return double.IsFinite(inkFlow) ? Math.Clamp(inkFlow, 0.0, 1.0) : 0.5;
    }

    private static DrawingBrush? BuildInkOpacityMask(
        Rect bounds,
        double inkFlow,
        Vector strokeDirection,
        double brushSize,
        int seed,
        double? wetnessStart,
        double? wetnessEnd)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }
        inkFlow = ResolveInkFlow(inkFlow);
        int tileSize = (int)Math.Round(Math.Clamp(brushSize * 2.2, 18, 90));
        double dryFactor = ResolveInkDryFactor(inkFlow, wetnessStart, wetnessEnd);
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
        InkMaskPainters.ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 0, translationJitterDip: 0);
        texture.Freeze();

        var radial = InkMaskPainters.BuildRadialFalloff(bounds, inkFlow);

        var group = new DrawingGroup();
        group.Children.Add(new GeometryDrawing(MediaBrushes.White, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(radial, null, new RectangleGeometry(bounds)));
        group.Children.Add(new GeometryDrawing(texture, null, new RectangleGeometry(bounds)));
        group.Freeze();
        return new DrawingBrush(group) { Stretch = Stretch.None };
    }


}
