using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfCursor = System.Windows.Input.Cursor;

namespace ClassroomToolkit.App.Utilities;

/// <summary>
/// 自定义光标生成器 - 为画笔工具创建形象美观的光标
/// </summary>
public static class CustomCursors
{
    private static WpfCursor? _brushCursor;
    private static WpfCursor? _eraserCursor;
    private static WpfCursor? _regionEraseCursor;

    /// <summary>
    /// 画笔光标 - 圆形光标，带中心点
    /// </summary>
    public static WpfCursor Brush
    {
        get
        {
            if (_brushCursor == null)
            {
                _brushCursor = CreateBrushCursor();
            }
            return _brushCursor;
        }
    }

    /// <summary>
    /// 橡皮擦光标 - 方形光标，带虚线边框
    /// </summary>
    public static WpfCursor Eraser
    {
        get
        {
            if (_eraserCursor == null)
            {
                _eraserCursor = CreateEraserCursor();
            }
            return _eraserCursor;
        }
    }

    /// <summary>
    /// 框选擦除光标 - 虚线矩形框光标
    /// </summary>
    public static WpfCursor RegionErase
    {
        get
        {
            if (_regionEraseCursor == null)
            {
                _regionEraseCursor = CreateRegionEraseCursor();
            }
            return _regionEraseCursor;
        }
    }

    private static WpfCursor CreateBrushCursor()
    {
        // 使用系统光标 Pen
        return System.Windows.Input.Cursors.Pen;
    }

    private static WpfCursor CreateEraserCursor()
    {
        // 使用 ScrollAll 光标
        return System.Windows.Input.Cursors.ScrollAll;
    }

    private static WpfCursor CreateRegionEraseCursor()
    {
        // 使用 UpArrow 光标
        return System.Windows.Input.Cursors.UpArrow;
    }
}
