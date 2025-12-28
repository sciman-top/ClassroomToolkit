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
    public double MinWidthFactor { get; set; } = 0.3;
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
    // 新增：坐标平滑状态
    private WpfPoint _smoothedPos;

    private readonly BrushPhysicsConfig _config = BrushPhysicsConfig.DefaultSmooth;

    public bool IsActive => _isActive;

    public void Initialize(WpfColor color, double baseSize, double opacity)
    {
        _color = color;
        _baseSize = baseSize;
        _smoothedWidth = baseSize * 0.8;
        _smoothedPos = new WpfPoint(0, 0); // 将在 OnDown 时初始化
    }

    public void OnDown(WpfPoint point)
    {
        _points.Clear();
        _isActive = true;
        _lastTimestamp = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;

        _smoothedWidth = _baseSize * 0.5; 
        _smoothedPos = point; // 初始化平滑坐标
        
        _points.Add(new StrokePoint(point, _smoothedWidth));
    }

    public void OnMove(WpfPoint point)
    {
        if (!_isActive) return;

        // --- Step 3: 输入平滑 (Input Smoothing) ---
        // 使用简单的指数移动平均 (EMA) 平滑坐标，减少手抖
        // 坐标平滑因子：0.7 (反应较快)，太大会有延迟感
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
        
        // 收笔处理
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
        // --- Step 1: 修复镂空 (NonZero FillRule) ---
        // 确保重叠部分被填充而不是镂空
        geometry.FillRule = FillRule.NonZero;

        using (var ctx = geometry.Open())
        {
            var leftEdge = new List<WpfPoint>();
            var rightEdge = new List<WpfPoint>();

            // 起笔扇形
            if (_config.SimulateStartCap && _points.Count > 1)
            {
                var p0 = _points[0];
                var p1 = _points[1];
                var dir = p1.Position - p0.Position;
                dir.Normalize();
                var normal = new Vector(-dir.Y, dir.X); 
                
                double capWidth = _baseSize * _config.MaxWidthFactor * 0.8;
                var center = p0.Position - dir * (capWidth * 0.2); 
                
                leftEdge.Add(center + normal * (capWidth * 0.5));
                leftEdge.Add(center - dir * (capWidth * 0.5)); 
                rightEdge.Add(center - normal * (capWidth * 0.5));
            }
            else
            {
                var p0 = _points[0];
                var p1 = _points[1];
                var normal = GetNormal(p0.Position, p1.Position);
                AddRibbonPoints(p0.Position, normal, p0.Width, leftEdge, rightEdge);
            }

            // 中段生成
            for (int i = 0; i < _points.Count - 1; i++)
            {
                var p0 = _points[i];
                var p1 = _points[i+1];

                if (i == _points.Count - 2)
                {
                    var normal = GetNormal(p0.Position, p1.Position);
                    AddRibbonPoints(p1.Position, normal, p1.Width, leftEdge, rightEdge);
                }
                else
                {
                    var p2 = _points[i+2];
                    
                    var startPos = (i == 0) ? p0.Position : Mid(p0.Position, p1.Position);
                    var endPos = Mid(p1.Position, p2.Position);
                    var controlPos = p1.Position;

                    var startWidth = (i == 0) ? p0.Width : (p0.Width + p1.Width) / 2.0;
                    var endWidth = (p1.Width + p2.Width) / 2.0;
                    var controlWidth = p1.Width;

                    TessellateBezier(startPos, controlPos, endPos, startWidth, controlWidth, endWidth, leftEdge, rightEdge);
                }
            }

            // 构建轮廓
            if (leftEdge.Count > 0)
            {
                ctx.BeginFigure(leftEdge[0], true, true);
                for (int i = 1; i < leftEdge.Count; i++) ctx.LineTo(leftEdge[i], true, true);
                for (int i = rightEdge.Count - 1; i >= 0; i--) ctx.LineTo(rightEdge[i], true, true);
            }
        }
        return geometry;
    }

    private void TessellateBezier(WpfPoint start, WpfPoint control, WpfPoint end, 
                                  double wStart, double wControl, double wEnd, 
                                  List<WpfPoint> lefts, List<WpfPoint> rights)
    {
        const int steps = 6; 
        
        // --- Step 2: 尖角/毛刺处理 (Round Joins Logic) ---
        // 我们不直接计算每一点的法线，而是检查法线是否突变。
        // 由于贝塞尔曲线本身是连续的，只要控制点不重合，法线变化通常是平滑的。
        // 真正产生毛刺的是当宽度很大且曲率很急时（内侧边缘打结）。
        
        // 为了简单高效，我们在这里限制 Offset 的最大变化率，
        // 或者简单地接受偶尔的自相交（因为 FillRule 已经是 NonZero 了，自相交会被填满，不会露白）。
        // 如果需要严格的 Round Join，需要更复杂的几何裁剪。
        // 鉴于这是实时书写，NonZero FillRule 通常足以掩盖内侧打结的问题。

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