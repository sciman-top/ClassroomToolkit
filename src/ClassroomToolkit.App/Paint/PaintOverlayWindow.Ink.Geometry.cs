using System;
using System.Collections.Generic;
using System.Buffers;
using System.Diagnostics;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
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
    private static string? ExcludeGeometry(string geometryPath, Geometry eraser)
    {
        if (string.IsNullOrWhiteSpace(geometryPath))
        {
            return null;
        }
        var geometry = InkGeometrySerializer.Deserialize(geometryPath);
        if (geometry == null)
        {
            return null;
        }
        if (!geometry.Bounds.IntersectsWith(eraser.Bounds))
        {
            return geometryPath;
        }
        var combined = Geometry.Combine(geometry, eraser, GeometryCombineMode.Exclude, null);
        if (combined == null || combined.Bounds.IsEmpty)
        {
            return string.Empty;
        }
        return InkGeometrySerializer.Serialize(combined);
    }

    private static Geometry? UnionGeometries(IReadOnlyList<Geometry> geometries)
    {
        if (geometries.Count == 0)
        {
            return null;
        }

        Geometry combined = geometries[0];
        for (int i = 1; i < geometries.Count; i++)
        {
            combined = new CombinedGeometry(GeometryCombineMode.Union, combined, geometries[i]);
        }
        combined.Freeze();
        return combined;
    }

    private static Int32Rect IntersectRects(Int32Rect a, Int32Rect b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Min(a.Y + a.Height, b.Y + b.Height);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return new Int32Rect(0, 0, 0, 0);
        }
        return new Int32Rect(x, y, width, height);
    }

    private static Int32Rect UnionRects(Int32Rect a, Int32Rect b)
    {
        int x = Math.Min(a.X, b.X);
        int y = Math.Min(a.Y, b.Y);
        int right = Math.Max(a.X + a.Width, b.X + b.Width);
        int bottom = Math.Max(a.Y + a.Height, b.Y + b.Height);
        int width = right - x;
        int height = bottom - y;
        if (width <= 0 || height <= 0)
        {
            return new Int32Rect(0, 0, 0, 0);
        }
        return new Int32Rect(x, y, width, height);
    }

    private static void UpdateSelectionRect(WpfRectangle rect, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        Canvas.SetLeft(rect, left);
        Canvas.SetTop(rect, top);
        rect.Width = Math.Max(InkGeometryDefaults.MinSelectionRectSideDip, width);
        rect.Height = Math.Max(InkGeometryDefaults.MinSelectionRectSideDip, height);
    }

    private static Rect BuildRegionRect(WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        return new Rect(
            left,
            top,
            Math.Max(InkGeometryDefaults.MinSelectionRectSideDip, width),
            Math.Max(InkGeometryDefaults.MinSelectionRectSideDip, height));
    }

    private static List<InkStrokeData> CloneInkStrokes(IEnumerable<InkStrokeData> source)
    {
        return source.Select(stroke => new InkStrokeData
        {
            Type = stroke.Type,
            BrushStyle = stroke.BrushStyle,
            GeometryPath = stroke.GeometryPath,
            ColorHex = stroke.ColorHex,
            Opacity = stroke.Opacity,
            BrushSize = stroke.BrushSize,
            MaskSeed = stroke.MaskSeed,
            InkFlow = stroke.InkFlow,
            StrokeDirectionX = stroke.StrokeDirectionX,
            StrokeDirectionY = stroke.StrokeDirectionY,
            CalligraphyRenderMode = stroke.CalligraphyRenderMode,
            ReferenceWidth = stroke.ReferenceWidth,
            ReferenceHeight = stroke.ReferenceHeight,
            Ribbons = stroke.Ribbons.Select(ribbon => new InkRibbonData
            {
                GeometryPath = ribbon.GeometryPath,
                Opacity = ribbon.Opacity,
                RibbonT = ribbon.RibbonT
            }).ToList(),
            CalligraphyInkBloomEnabled = stroke.CalligraphyInkBloomEnabled,
            CalligraphySealEnabled = stroke.CalligraphySealEnabled,
            CalligraphyOverlayOpacityThreshold = stroke.CalligraphyOverlayOpacityThreshold,
            Blooms = stroke.Blooms.Select(bloom => new InkBloomData
            {
                GeometryPath = bloom.GeometryPath,
                Opacity = bloom.Opacity
            }).ToList()
        }).ToList();
    }

    private static string ToHex(MediaColor color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }

    private static bool TryParseStrokeColor(string? colorHex, out MediaColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return false;
        }

        var parseResult = PaintActionInvoker.TryInvoke(() =>
        {
            var parsed = System.Windows.Media.ColorConverter.ConvertFromString(colorHex);
            if (parsed is MediaColor value)
            {
                return (Parsed: true, Color: value);
            }

            return (Parsed: false, Color: default(MediaColor));
        }, fallback: (Parsed: false, Color: default(MediaColor)));
        if (!parseResult.Parsed)
        {
            return false;
        }

        color = parseResult.Color;
        return true;
    }

    private Shape? CreateShape(PaintShapeType type)
    {
        return type switch
        {
            PaintShapeType.None => null,
            PaintShapeType.Line => new Line(),
            PaintShapeType.DashedLine => new Line(),
            PaintShapeType.Rectangle => new WpfRectangle(),
            PaintShapeType.RectangleFill => new WpfRectangle(),
            PaintShapeType.Ellipse => new Ellipse(),
            PaintShapeType.Path => new WpfPath(),
            _ => null
        };
    }

    private void ApplyShapeStyle(Shape shape)
    {
        var stroke = new SolidColorBrush(EffectiveBrushColor());
        stroke.Freeze();
        shape.Stroke = stroke;
        shape.StrokeThickness = Math.Max(InkGeometryDefaults.MinShapeStrokeThicknessDip, _brushSize);
        shape.StrokeStartLineCap = PenLineCap.Round;
        shape.StrokeEndLineCap = PenLineCap.Round;
        shape.StrokeLineJoin = PenLineJoin.Round;
        if (_shapeType == PaintShapeType.DashedLine)
        {
            shape.StrokeDashArray = new DoubleCollection { 6, 4 };
        }
        shape.Fill = null;
        shape.IsHitTestVisible = false;
    }

    private static void UpdateShape(Shape shape, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (shape is Line line)
        {
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
        }
        else
        {
            Canvas.SetLeft(shape, left);
            Canvas.SetTop(shape, top);
            shape.Width = Math.Max(InkGeometryDefaults.MinShapeRectSideDip, width);
            shape.Height = Math.Max(InkGeometryDefaults.MinShapeRectSideDip, height);
        }
    }

    private Geometry? BuildShapeGeometry(PaintShapeType type, WpfPoint start, WpfPoint end)
    {
        var rect = new Rect(start, end);
        return type switch
        {
            PaintShapeType.Line => new LineGeometry(start, end),
            PaintShapeType.DashedLine => new LineGeometry(start, end),
            PaintShapeType.Rectangle => new RectangleGeometry(rect),
            PaintShapeType.RectangleFill => new RectangleGeometry(rect),
            PaintShapeType.Ellipse => new EllipseGeometry(rect),
            _ => null
        };
    }

    private MediaPen BuildShapePen()
    {
        var brush = new SolidColorBrush(EffectiveBrushColor());
        brush.Freeze();
        var pen = new MediaPen(brush, Math.Max(InkGeometryDefaults.MinPenThicknessDip, _brushSize))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        if (_shapeType == PaintShapeType.DashedLine)
        {
            pen.DashStyle = new DashStyle(new double[] { 6, 4 }, 0);
            pen.DashCap = PenLineCap.Round;
        }
        pen.Freeze();
        return pen;
    }

    private Geometry? BuildEraserGeometry(WpfPoint start, WpfPoint end)
    {
        var radius = Math.Max(InkGeometryDefaults.MinEraserRadiusDip, _eraserSize * 0.5);
        var delta = end - start;
        if (delta.Length < InkGeometryDefaults.EraserTapDistanceThresholdDip)
        {
            return new EllipseGeometry(start, radius, radius);
        }
        var path = new StreamGeometry();
        using (var ctx = path.Open())
        {
            ctx.BeginFigure(start, isFilled: false, isClosed: false);
            ctx.LineTo(end, isStroked: true, isSmoothJoin: true);
        }
        var pen = new MediaPen(MediaBrushes.Black, Math.Max(InkGeometryDefaults.MinPenThicknessDip, _eraserSize))
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round
        };
        return path.GetWidenedPathGeometry(pen);
    }
}

