using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using ClassroomToolkit.App.Ink;
using WpfPoint = System.Windows.Point;
using MediaBrush = System.Windows.Media.Brush;
using MediaPen = System.Windows.Media.Pen;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// 绘图功能（笔刷、形状、橡皮擦、墨迹存储与渲染）
/// </summary>
public partial class PaintOverlayWindow
{
    // 绘图相关方法将从主文件迁移到此处
    // 包括：BeginBrushStroke, UpdateBrushStroke, EndBrushStroke,
    // BeginEraser, UpdateEraser, EndEraser,
    // BeginShape, UpdateShapePreview, EndShape,
    // BeginRegionSelection, UpdateRegionSelection, EndRegionSelection,
    // RecordBrushStroke, RecordShapeStroke,
    // RenderInkCore, RenderInkEdge, RenderInkSeal, RenderCalligraphyComposite,
    // CommitGeometryFill, CommitGeometryStroke, EraseGeometry,
    // EnsureRasterSurface, ClearSurface, CopyBitmapToSurface,
    // PushHistory, RestoreSnapshot, Undo
}
