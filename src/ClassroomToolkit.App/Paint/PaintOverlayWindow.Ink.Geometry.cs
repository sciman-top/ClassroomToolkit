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

    private static Shape? CreateShape(PaintShapeType type)
    {
        return type switch
        {
            PaintShapeType.None => null,
            PaintShapeType.Line => new Line(),
            PaintShapeType.DashedLine => new Line(),
            PaintShapeType.Arrow => new WpfPath(),
            PaintShapeType.DashedArrow => new WpfPath(),
            PaintShapeType.Rectangle => new WpfRectangle(),
            PaintShapeType.RectangleFill => new WpfRectangle(),
            PaintShapeType.Ellipse => new Ellipse(),
            PaintShapeType.Triangle => new WpfPath(),
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
        shape.StrokeStartLineCap = PenLineCap.Flat;
        shape.StrokeEndLineCap = PenLineCap.Flat;
        shape.StrokeLineJoin = PenLineJoin.Miter;
        if (_shapeType == PaintShapeType.DashedLine || _shapeType == PaintShapeType.DashedArrow)
        {
            shape.StrokeDashArray = new DoubleCollection { 6, 4 };
        }
        if (_shapeType == PaintShapeType.Arrow || _shapeType == PaintShapeType.DashedArrow)
        {
            var fillOnly = new SolidColorBrush(EffectiveBrushColor());
            fillOnly.Freeze();
            shape.Fill = fillOnly;
            shape.Stroke = null;
            shape.StrokeDashArray = null;
            shape.IsHitTestVisible = false;
            return;
        }

        if (_shapeType == PaintShapeType.RectangleFill || _shapeType == PaintShapeType.DashedArrow)
        {
            var fill = new SolidColorBrush(EffectiveBrushColor());
            fill.Freeze();
            shape.Fill = fill;
        }
        else
        {
            shape.Fill = null;
        }
        shape.IsHitTestVisible = false;
    }

    private void UpdateShape(Shape shape, PaintShapeType type, WpfPoint start, WpfPoint end)
    {
        var left = Math.Min(start.X, end.X);
        var top = Math.Min(start.Y, end.Y);
        var width = Math.Abs(end.X - start.X);
        var height = Math.Abs(end.Y - start.Y);
        if (type == PaintShapeType.Line || type == PaintShapeType.DashedLine)
        {
            if (shape is not Line line)
            {
                return;
            }
            line.X1 = start.X;
            line.Y1 = start.Y;
            line.X2 = end.X;
            line.Y2 = end.Y;
            return;
        }

        if (type == PaintShapeType.Arrow)
        {
            if (shape is not WpfPath path)
            {
                return;
            }
            path.Data = BuildArrowGeometry(start, end);
            return;
        }
        if (type == PaintShapeType.DashedArrow)
        {
            if (shape is not WpfPath path)
            {
                return;
            }
            path.Data = BuildDashedArrowGeometry(start, end);
            return;
        }

        if (type == PaintShapeType.Triangle)
        {
            if (shape is not WpfPath path)
            {
                return;
            }
            path.Data = BuildTrianglePreviewGeometry(start, end);
            return;
        }

        if (shape is not FrameworkElement element)
        {
            return;
        }
        Canvas.SetLeft(element, left);
        Canvas.SetTop(element, top);
        element.Width = Math.Max(InkGeometryDefaults.MinShapeRectSideDip, width);
        element.Height = Math.Max(InkGeometryDefaults.MinShapeRectSideDip, height);
    }

    private Geometry? BuildShapeGeometry(PaintShapeType type, WpfPoint start, WpfPoint end)
    {
        var rect = new Rect(start, end);
        return type switch
        {
            PaintShapeType.Line => new LineGeometry(start, end),
            PaintShapeType.DashedLine => new LineGeometry(start, end),
            PaintShapeType.Arrow => BuildArrowGeometry(start, end),
            PaintShapeType.DashedArrow => BuildDashedArrowGeometry(start, end),
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
            StartLineCap = PenLineCap.Flat,
            EndLineCap = PenLineCap.Flat,
            LineJoin = PenLineJoin.Miter
        };
        if (_shapeType == PaintShapeType.DashedLine || _shapeType == PaintShapeType.DashedArrow)
        {
            pen.DashStyle = new DashStyle(new double[] { 6, 4 }, 0);
            pen.DashCap = PenLineCap.Flat;
        }
        pen.Freeze();
        return pen;
    }

    private static Geometry BuildTrianglePreviewGeometry(WpfPoint p1, WpfPoint p2)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(p1, isFilled: false, isClosed: false);
        ctx.LineTo(p2, isStroked: true, isSmoothJoin: false);
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildTriangleGeometry(WpfPoint p1, WpfPoint p2, WpfPoint p3)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(p1, isFilled: false, isClosed: true);
        ctx.LineTo(p2, isStroked: true, isSmoothJoin: false);
        ctx.LineTo(p3, isStroked: true, isSmoothJoin: false);
        geometry.Freeze();
        return geometry;
    }

    private static Geometry BuildTriangleInteractivePreviewGeometry(WpfPoint p1, WpfPoint p2, WpfPoint cursor)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(p1, isFilled: false, isClosed: false);
        ctx.LineTo(p2, isStroked: true, isSmoothJoin: false);
        ctx.LineTo(cursor, isStroked: true, isSmoothJoin: false);
        ctx.LineTo(p1, isStroked: true, isSmoothJoin: false);
        geometry.Freeze();
        return geometry;
    }

    private Geometry BuildArrowGeometry(WpfPoint start, WpfPoint end)
    {
        var direction = end - start;
        var length = direction.Length;
        if (length < InkGeometryDefaults.MinSelectionRectSideDip)
        {
            return new LineGeometry(start, end);
        }

        direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);
        double minHeadLength = Math.Max(_brushSize * 3.0, 9.0);
        double maxHeadLength = Math.Max(minHeadLength, 28.0);
        double headLength = Math.Clamp(length * 0.3, minHeadLength, maxHeadLength);
        double shaftHalfWidth = Math.Max(_brushSize * 0.5, 2.0);
        double headHalfWidth = Math.Max(headLength * 0.58, shaftHalfWidth * 1.8);
        var headBase = end - (direction * headLength);
        var notch = headBase + (direction * Math.Max(headLength * 0.32, shaftHalfWidth * 1.5));

        var shaftTopStart = start + (perpendicular * shaftHalfWidth);
        var shaftTopEnd = notch + (perpendicular * shaftHalfWidth);
        var shaftBottomEnd = notch - (perpendicular * shaftHalfWidth);
        var shaftBottomStart = start - (perpendicular * shaftHalfWidth);

        var headTop = headBase + (perpendicular * headHalfWidth);
        var headBottom = headBase - (perpendicular * headHalfWidth);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Single closed outline removes antialias seam between shaft/head pieces.
            ctx.BeginFigure(shaftTopStart, isFilled: true, isClosed: true);
            ctx.LineTo(shaftTopEnd, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(headTop, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(end, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(headBottom, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(shaftBottomEnd, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(shaftBottomStart, isStroked: true, isSmoothJoin: false);
        }
        geometry.Freeze();
        return geometry;
    }

    private Geometry BuildDashedArrowGeometry(WpfPoint start, WpfPoint end)
    {
        var direction = end - start;
        var length = direction.Length;
        if (length < InkGeometryDefaults.MinSelectionRectSideDip)
        {
            return new LineGeometry(start, end);
        }

        direction.Normalize();
        var perpendicular = new Vector(-direction.Y, direction.X);
        double minHeadLength = Math.Max(_brushSize * 3.0, 9.0);
        double maxHeadLength = Math.Max(minHeadLength, 28.0);
        double headLength = Math.Clamp(length * 0.3, minHeadLength, maxHeadLength);
        double shaftHalfWidth = Math.Max(_brushSize * 0.5, 2.0);
        double headHalfWidth = Math.Max(headLength * 0.58, shaftHalfWidth * 1.8);
        var headBase = end - (direction * headLength);
        var notch = headBase + (direction * Math.Max(headLength * 0.32, shaftHalfWidth * 1.5));

        var headTop = headBase + (perpendicular * headHalfWidth);
        var headBottom = headBase - (perpendicular * headHalfWidth);

        var group = new GeometryGroup
        {
            FillRule = FillRule.Nonzero
        };
        var shaftLength = Math.Max(0.0, (notch - start).Length);
        var strokeThickness = Math.Max(InkGeometryDefaults.MinShapeStrokeThicknessDip, _brushSize);
        var dashLength = Math.Max(2.0, strokeThickness * 6.0);
        var gapLength = Math.Max(1.0, strokeThickness * 4.0);
        var segments = new List<(double Start, double End)>();
        for (double cursor = 0.0; cursor < shaftLength; cursor += (dashLength + gapLength))
        {
            var segmentStartDistance = cursor;
            var segmentEndDistance = Math.Min(cursor + dashLength, shaftLength);
            if ((segmentEndDistance - segmentStartDistance) <= 0.1)
            {
                continue;
            }
            segments.Add((segmentStartDistance, segmentEndDistance));
        }

        // Draw all leading dashed shaft segments, reserve the last segment for
        // a unified "shaft+head" polygon to eliminate seam holes.
        for (int i = 0; i < Math.Max(0, segments.Count - 1); i++)
        {
            var segmentStartDistance = segments[i].Start;
            var segmentEndDistance = segments[i].End;

            var segmentStart = start + (direction * segmentStartDistance);
            var segmentEnd = start + (direction * segmentEndDistance);
            var segmentTopStart = segmentStart + (perpendicular * shaftHalfWidth);
            var segmentTopEnd = segmentEnd + (perpendicular * shaftHalfWidth);
            var segmentBottomEnd = segmentEnd - (perpendicular * shaftHalfWidth);
            var segmentBottomStart = segmentStart - (perpendicular * shaftHalfWidth);

            var segment = new StreamGeometry();
            using (var segmentCtx = segment.Open())
            {
                segmentCtx.BeginFigure(segmentTopStart, isFilled: true, isClosed: true);
                segmentCtx.LineTo(segmentTopEnd, isStroked: true, isSmoothJoin: false);
                segmentCtx.LineTo(segmentBottomEnd, isStroked: true, isSmoothJoin: false);
                segmentCtx.LineTo(segmentBottomStart, isStroked: true, isSmoothJoin: false);
            }
            segment.Freeze();
            group.Children.Add(segment);
        }

        var connectorStartDistance = segments.Count > 0 ? segments[^1].Start : 0.0;
        var connectorStart = start + (direction * connectorStartDistance);
        var connectorTopStart = connectorStart + (perpendicular * shaftHalfWidth);
        var connectorBottomStart = connectorStart - (perpendicular * shaftHalfWidth);
        var connectorTopEnd = notch + (perpendicular * shaftHalfWidth);
        var connectorBottomEnd = notch - (perpendicular * shaftHalfWidth);

        var connectorAndHead = new StreamGeometry();
        using (var ctx = connectorAndHead.Open())
        {
            ctx.BeginFigure(connectorTopStart, isFilled: true, isClosed: true);
            ctx.LineTo(connectorTopEnd, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(headTop, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(end, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(headBottom, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(connectorBottomEnd, isStroked: true, isSmoothJoin: false);
            ctx.LineTo(connectorBottomStart, isStroked: true, isSmoothJoin: false);
        }
        connectorAndHead.Freeze();
        group.Children.Add(connectorAndHead);
        group.Freeze();
        return group;
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
