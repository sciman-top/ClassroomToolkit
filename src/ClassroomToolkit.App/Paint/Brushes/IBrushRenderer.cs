using System.Windows;
using System.Windows.Media;
using WpfPoint = System.Windows.Point;
using WpfColor = System.Windows.Media.Color;

namespace ClassroomToolkit.App.Paint.Brushes;

public interface IBrushRenderer
{
    void Initialize(WpfColor color, double baseSize, double opacity);
    void OnDown(WpfPoint point);
    void OnMove(WpfPoint point);
    void OnUp(WpfPoint point);
    void Render(DrawingContext dc);
    void Reset();
    bool IsActive { get; }
    Geometry? GetLastStrokeGeometry();
}
