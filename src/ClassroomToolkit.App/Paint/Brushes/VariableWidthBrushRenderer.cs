using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint.Brushes;

public class BrushPhysicsConfig
{
    public double MinWidthFactor { get; set; } = 0.25;
    public double MaxWidthFactor { get; set; } = 1.3;
    public double WidthSmoothing { get; set; } = 0.85;
    public bool SimulateStartCap { get; set; } = true;
    public bool SimulateEndTaper { get; set; } = true;
    public double VelocityThreshold { get; set; } = 2.0;
    
    public static BrushPhysicsConfig DefaultSmooth => new();
}

public class VariableWidthBrushRenderer : IBrushRenderer
{
    private struct StrokePoint
    {
        public WpfPoint Position;
        public double Width;

        public StrokePoint(WpfPoint pos, double width)
        {
            Position = pos;
            Width = width;
        }
    }

    private readonly List<StrokePoint> _points = new();
    private WpfColor _color;
    private double _baseSize;
    private bool _isActive;
    private long _lastTimestamp;
    
    private double _smoothedWidth; 
    private WpfPoint _smoothedPos;

    private readonly BrushPhysicsConfig _config = BrushPhysicsConfig.DefaultSmooth;

    public bool IsActive => _isActive;

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _smoothedWidth = baseSize * 0.8;
        _smoothedPos = new WpfPoint(0, 0); 
    }

    public void OnDown(WpfPoint point)
    {
        _points.Clear();
        _isActive = true;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        _smoothedWidth = _baseSize * 0.5; 
        _smoothedPos = point; 
        
        _points.Add(new StrokePoint(point, _smoothedWidth));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        // 坐标平滑 (EMA)
        double posAlpha = 0.7;
        _smoothedPos = new WpfPoint(
            _smoothedPos.X * (1 - posAlpha) + point.X * posAlpha,
            _smoothedPos.Y * (1 - posAlpha) + point.Y * posAlpha
        );

        var lastPt = _points.Last();
        var dist = (_smoothedPos - lastPt.Position).Length;

        // 去噪：忽略过小的移动
        if (dist < 2.0) return;

        var now = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
        var dt = now - _lastTimestamp;
        if (dt < 1) dt = 1;

        double velocity = dist / dt;
        double targetWidth = CalculateTargetWidth(velocity);

        double widthAlpha = _config.WidthSmoothing;
        _smoothedWidth = (_smoothedWidth * widthAlpha) + (targetWidth * (1.0 - widthAlpha));

        _points.Add(new StrokePoint(_smoothedPos, _smoothedWidth));
        _lastTimestamp = now;
    }

    public void OnUp(WpfPoint point)
    {
        if (!_isActive) return;
        
        var last = _points.Last();
        var dir = point - last.Position;
        if (dir.Length > 0.1) dir.Normalize();
        else dir = new Vector(1, 0);
        
        var extension = _baseSize * 0.5;
        var endPos = point + dir * extension;

        _points.Add(new StrokePoint(endPos, 0.1));
        _isActive = false;
    }

    public void Reset()
    {
        _points.Clear();
        _isActive = false;
    }

    public void Render(DrawingContext dc)
    {
        if (_points.Count < 2) return;

        var geometry = GenerateSmoothGeometry();
        if (geometry != null)
        {
            var brush = new SolidColorBrush(_color);
            brush.Freeze();
            dc.DrawGeometry(brush, null, geometry);
        }
    }

    public Geometry? GetLastStrokeGeometry()
    {
        if (_points.Count < 2) return null;
        var geo = GenerateSmoothGeometry();
        if (geo != null) geo.Freeze();
        return geo;
    }

    private double CalculateTargetWidth(double velocity)
    {
        var t = Math.Min(velocity / _config.VelocityThreshold, 1.0); 
        var factor = 1.0 - (t * t); 
        var range = _config.MaxWidthFactor - _config.MinWidthFactor;
        return _baseSize * (_config.MinWidthFactor + (range * factor));
    }

    private Geometry? GenerateSmoothGeometry()
    {
        if (_points.Count < 2) return null;

        var geometry = new StreamGeometry();
        geometry.FillRule = FillRule.Nonzero;

        using (var ctx = geometry.Open())
        {
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();

            // --- 阶段 1：生成左右边缘点 ---
            // 使用贝塞尔插值生成高密度的点云
            for (int i = 0; i < _points.Count - 1; i++)
            {
                var p0 = _points[i];
                var p1 = _points[i+1];

                if (i == _points.Count - 2)
                {
                    // 最后一段
                    var normal = GetNormal(p0.Position, p1.Position);
                    AddRibbonPoints(p1.Position, normal, p1.Width, leftEdge, rightEdge);
                }
                else
                {
                    var p2 = _points[i+2];
                    var startPos = (i == 0) ? p0.Position : Mid(p0.Position, p1.Position);
                    var endPos = Mid(p1.Position, p2.Position);
                    var controlPos = p1.Position;

                    var wStart = (i == 0) ? p0.Width : (p0.Width + p1.Width) / 2.0;
                    var wEnd = (p1.Width + p2.Width) / 2.0;
                    var wControl = p1.Width;

                    TessellateBezier(startPos, controlPos, endPos, wStart, wControl, wEnd, leftEdge, rightEdge);
                }
            }

            // --- 阶段 2：过滤倒刺 (Spike Removal) ---
            // 简单的距离过滤器：如果下一个点离当前点太近（或者回头了），说明内侧打结了
            FilterLoops(leftEdge);
            FilterLoops(rightEdge);

            // --- 阶段 3：构建 Path ---
            if (leftEdge.Count > 0)
            {
                ctx.BeginFigure(leftEdge[0], true, true);
                
                // 左边缘
                for (int i = 1; i < leftEdge.Count; i++) ctx.LineTo(leftEdge[i], true, true);
                
                // 收笔圆头 (Arc To Right Edge End)
                // 从左边缘最后一点画一个半圆到右边缘最后一点
                var lastLeft = leftEdge.Last();
                var lastRight = rightEdge.Last();
                // 使用较小的半径，防止画出大圆圈
                ctx.ArcTo(lastRight, new Size(_baseSize/2, _baseSize/2), 0, false, SweepDirection.Clockwise, true, true);

                // 右边缘 (倒序)
                for (int i = rightEdge.Count - 2; i >= 0; i--) ctx.LineTo(rightEdge[i], true, true);

                // 起笔圆头 (Arc Back To Start)
                var firstLeft = leftEdge[0];
                var firstRight = rightEdge[0];
                ctx.ArcTo(firstLeft, new Size(_baseSize/2, _baseSize/2), 0, false, SweepDirection.Clockwise, true, true);
            }
        }
        return geometry;
    }

    /// <summary>
    /// 简单的去倒刺逻辑：移除距离过近的点
    /// </summary>
    private void FilterLoops(List<WpfPoint> edge)
    {
        if (edge.Count < 3) return;
        
        // 我们新建一个列表，只保留“有效推进”的点
        // 这是一种简化的 Loop Removal，不是严格的几何裁剪
        // 但对于手写笔画来说，大部分倒刺都是因为内侧点“退回”造成的
        
        for (int i = edge.Count - 2; i >= 1; i--)
        {
            var prev = edge[i - 1];
            var curr = edge[i];
            var next = edge[i + 1];

            // 检查锐角：如果 prev->curr 和 curr->next 夹角过小 (<30度)，说明这里是个尖刺
            var v1 = curr - prev;
            var v2 = next - curr;
            
            if (v1.Length < 0.1 || v2.Length < 0.1) continue; // 忽略重合点

            double angle = Vector.AngleBetween(v1, v2);
            // AngleBetween 返回 -180 到 180
            // 尖刺通常意味着角度接近 180 (反向)
            if (Math.Abs(angle) > 135) 
            {
                // 这是一个折返点，移除它
                edge.RemoveAt(i);
            }
        }
    }

    private void TessellateBezier(WpfPoint start, WpfPoint control, WpfPoint end, 
                                  double wStart, double wControl, double wEnd, 
                                  List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        const int steps = 6; 

        for (int i = 1; i <= steps; i++)
        {
            double t = i / (double)steps;
            double u = 1.0 - t;

            double x = u * u * start.X + 2 * u * t * control.X + t * t * end.X;
            double y = u * u * start.Y + 2 * u * t * control.Y + t * t * end.Y;
            var pos = new WpfPoint(x, y);

            double tx = 2 * u * (control.X - start.X) + 2 * t * (end.X - control.X);
            double ty = 2 * u * (control.Y - start.Y) + 2 * t * (end.Y - control.Y);
            
            var normal = new Vector(-ty, tx);
            if (normal.LengthSquared > 0.0001) normal.Normalize();

            double width = u * u * wStart + 2 * u * t * wControl + t * t * wEnd;

            // --- 倒刺预防 (Angle Limiting) ---
            // 不再无脑延伸，我们检查这个偏移点是否与骨架线“打架”
            // 但在 Tessellate 阶段很难获取全局上下文。
            // 最好的办法是在生成后统一 Filter (见 FilterLoops)
            
            AddRibbonPoints(pos, normal, width, lefts, rights);
        }
    }

    private void AddRibbonPoints(WpfPoint center, Vector normal, double width, List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        var half = width * 0.5;
        lefts.Add(center + normal * half);
        rights.Add(center - normal * half);
    }

    private static WpfPoint Mid(WpfPoint a, WpfPoint b)
    {
        return new WpfPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
    }

    private static Vector GetNormal(WpfPoint a, WpfPoint b)
    {
        var dir = b - a;
        if (dir.LengthSquared < 0.0001) return new Vector(0, 1);
        dir.Normalize();
        return new Vector(-dir.Y, dir.X);
    }
}
