using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

/// <summary>
/// 照片模式功能（加载、缩放、平移、跨页显示）
/// </summary>
public partial class PaintOverlayWindow
{
    // 照片模式相关方法将从主文件迁移到此处
    // 包括：EnterPhotoMode, ExitPhotoMode, SetPhotoSequence, SetPhotoWindowMode,
    // ZoomPhoto, ZoomPhotoByFactor, ApplyPhotoScale,
    // TryBeginPhotoPan, UpdatePhotoPan, EndPhotoPan,
    // ApplyCrossPageBoundaryLimits, GetPageBitmap, GetNeighborPageBitmap,
    // ClearNeighborPages, ClearNeighborImageCache,
    // ShowPhotoContextMenu, ExecutePhotoMinimize,
    // SchedulePhotoTransformSave, FlushPhotoTransformSave
}
