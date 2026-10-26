using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private static DrawingBrush? BuildInkOpacityMask(
        Rect bounds,
        double inkFlow,
        Vector? strokeDirection,
        double brushSize,
        int seed,
        double? wetnessStart,
        double? wetnessEnd)
    {
        if (bounds.IsEmpty)
        {
            return null;
        }
        double safeInkFlow = InkStrokeRenderer.ResolveInkFlow(inkFlow);
        int tileSize = (int)Math.Round(Math.Clamp(brushSize * 2.2, 18, 90));
        int detailTileSize = (int)Math.Round(Math.Clamp(tileSize * 0.62, 12, 56));
        double dryFactor = InkStrokeRenderer.ResolveInkDryFactor(safeInkFlow, wetnessStart, wetnessEnd);
        bool hasFiniteWetness = wetnessStart.HasValue
            && wetnessEnd.HasValue
            && double.IsFinite(wetnessStart.Value)
            && double.IsFinite(wetnessEnd.Value);
        double wetnessDrop = hasFiniteWetness
            ? Math.Clamp(wetnessStart!.Value - wetnessEnd!.Value, 0.0, 1.0)
            : 0.0;
        double averageWetness = hasFiniteWetness
            ? Math.Clamp((wetnessStart!.Value + wetnessEnd!.Value) * 0.5, 0.0, 1.0)
            : 0.5;
        double baseAlpha = Lerp(0.74, 0.93, safeInkFlow);
        if (hasFiniteWetness)
        {
            // 湿度只做低幅材料调制；旧 payload 没有湿度时保持原纹理参数。
            baseAlpha = Math.Clamp(
                baseAlpha + ((averageWetness - 0.5) * 0.06) - (wetnessDrop * 0.025),
                0.68,
                0.96);
        }
        double variation = Lerp(0.1, 0.18, dryFactor)
            + (wetnessDrop * 0.04);
        variation = Math.Clamp(variation, 0.1, 0.24);
        double detailVariation = Lerp(0.05, 0.11, dryFactor * 0.7);
        int safeSeed = seed == 0 ? 17 : seed;
        int anchorX = (int)Math.Round(bounds.X * 0.35);
        int anchorY = (int)Math.Round(bounds.Y * 0.35);
        int effectiveSeed = HashCode.Combine(safeSeed, tileSize, anchorX, anchorY);
        int detailSeed = HashCode.Combine(safeSeed, detailTileSize, anchorY, anchorX, 97);
        var tile = InkNoiseTileCache.GetOrCreate(tileSize, baseAlpha, variation, effectiveSeed);
        var detailTile = InkNoiseTileCache.GetOrCreate(detailTileSize, baseAlpha, detailVariation, detailSeed);

        var texture = new ImageBrush(tile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X, bounds.Y, tileSize, tileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.58 + (safeInkFlow * 0.22) - (wetnessDrop * 0.025), 0.48, 0.92)
        };
        InkMaskPainters.ApplyInkTextureTransform(texture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 0, translationJitterDip: 0);
        texture.Freeze();

        var detailTexture = new ImageBrush(detailTile)
        {
            TileMode = TileMode.Tile,
            Viewport = new Rect(bounds.X + detailTileSize * 0.3, bounds.Y + detailTileSize * 0.2, detailTileSize, detailTileSize),
            ViewportUnits = BrushMappingMode.Absolute,
            Stretch = Stretch.None,
            Opacity = Math.Clamp(0.18 + (dryFactor * 0.14) + (wetnessDrop * 0.025), 0.12, 0.39)
        };
        double detailJitter = (Math.Abs(safeSeed) % 7) - 3;
        InkMaskPainters.ApplyInkTextureTransform(detailTexture, bounds, strokeDirection, dryFactor, angleOffsetDegrees: 90, translationJitterDip: detailJitter);
        detailTexture.Freeze();

        var radial = InkMaskPainters.BuildRadialFalloff(bounds, safeInkFlow);

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


}
