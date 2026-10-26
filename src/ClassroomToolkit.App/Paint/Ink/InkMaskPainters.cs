using System;
using System.Windows;
using System.Windows.Media;
using MediaColor = System.Windows.Media.Color;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// 墨迹不透明 mask 的共享绘制块（导出渲染与实况渲染两条路径共用）。
/// 三块逻辑的常量与公式即视觉契约：任何调整须同步 golden（InkFeel/导出坐标不变性）验证。
/// </summary>
internal static class InkMaskPainters
{
    private static double Lerp(double a, double b, double t)
    {
        return a + ((b - a) * t);
    }

    internal static bool IsInkMaskEligible(Geometry geometry, double brushSize)
    {
        if (geometry.Bounds.IsEmpty)
        {
            return false;
        }

        var bounds = geometry.Bounds;
        double minSize = Math.Max(brushSize * 1.0, 14.0);
        return bounds.Width >= minSize && bounds.Height >= minSize;
    }

    internal static RadialGradientBrush BuildRadialFalloff(Rect bounds, double inkFlow)
    {
        var centerOpacity = Math.Clamp(0.95 + (inkFlow * 0.05), 0.85, 1.0);
        var edgeOpacity = Math.Clamp(0.72 + (inkFlow * 0.08), 0.6, 0.9);
        var radial = new RadialGradientBrush
        {
            MappingMode = BrushMappingMode.Absolute,
            Center = new WpfPoint(bounds.X + (bounds.Width * 0.5), bounds.Y + (bounds.Height * 0.5)),
            GradientOrigin = new WpfPoint(bounds.X + (bounds.Width * 0.48), bounds.Y + (bounds.Height * 0.48)),
            RadiusX = bounds.Width * 0.55,
            RadiusY = bounds.Height * 0.55
        };
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)centerOpacity, 1, 1, 1), 0.0));
        radial.GradientStops.Add(new GradientStop(MediaColor.FromScRgb((float)edgeOpacity, 1, 1, 1), 1.0));
        radial.Freeze();
        return radial;
    }

    internal static void ApplyInkTextureTransform(
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

        double angle = (Math.Atan2(dir.Y, dir.X) * 180.0 / Math.PI) + angleOffsetDegrees;
        double centerX = bounds.X + (bounds.Width * 0.5);
        double centerY = bounds.Y + (bounds.Height * 0.5);
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
}
