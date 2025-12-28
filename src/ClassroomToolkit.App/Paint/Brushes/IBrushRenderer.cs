using System.Windows;
using System.Windows.Media;

namespace ClassroomToolkit.App.Paint.Brushes;

public interface IBrushRenderer
{
    void Initialize(Color color, double baseSize, double opacity);
    void OnDown(Point point);
    void OnMove(Point point);
    void OnUp(Point point);
    void Render(DrawingContext dc);
    void Reset();
    bool IsActive { get; }
    Geometry? GetLastStrokeGeometry();
}
