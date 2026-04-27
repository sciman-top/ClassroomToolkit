using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint.Brushes;

/// <summary>
/// 笔画点数据，用于部分删除功能
/// </summary>
public sealed class StrokePointData
{
    public WpfPoint Position { get; init; }
    public double Width { get; init; }

    public StrokePointData(WpfPoint position, double width)
    {
        Position = position;
        Width = width;
    }
}

[SuppressMessage("Design", "CA1002:Do not expose generic lists", Justification = "Brush renderers expose mutable point lists for partial-erase and quality tests without per-frame copies.")]
public interface IBrushRenderer
{
    void Initialize(WpfColor color, double baseSize, double opacity);
    void OnDown(BrushInputSample input);
    void OnMove(BrushInputSample input);
    void OnUp(BrushInputSample input);
    void Render(DrawingContext dc);
    void Reset();
    bool IsActive { get; }
    int GeometryVersion { get; }
    Geometry? GetLastStrokeGeometry();

    /// <summary>
    /// 获取最后一笔的原始点数据（用于部分删除）
    /// </summary>
    List<StrokePointData>? GetLastStrokePoints();
}
