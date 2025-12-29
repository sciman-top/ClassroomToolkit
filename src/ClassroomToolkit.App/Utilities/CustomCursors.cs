using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.Utilities;

/// <summary>
/// 自定义光标生成器 - 为画笔工具创建形象美观的光标
/// </summary>
public static class CustomCursors
{
    private static Cursor? _brushCursor;
    private static Cursor? _eraserCursor;
    private static Cursor? _regionEraseCursor;

    /// <summary>
    /// 画笔光标 - 圆形光标，带中心点
    /// </summary>
    public static Cursor Brush
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
    public static Cursor Eraser
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
    public static Cursor RegionErase
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

    private static Cursor CreateBrushCursor()
    {
        // 使用系统光标 Cross 并加上视觉效果
        return Cursors.Cross;
    }

    private static Cursor CreateEraserCursor()
    {
        // 使用自定义的方形光标
        return CreateCursorFromResource("Eraser");
    }

    private static Cursor CreateRegionEraseCursor()
    {
        return CreateCursorFromResource("RegionErase");
    }

    private static Cursor CreateCursorFromResource(string name)
    {
        // 由于创建 .cur 文件比较复杂，暂时返回系统光标
        // 未来可以通过嵌入 .cur 文件资源来实现
        return name switch
        {
            "Eraser" => Cursors.Hand,
            "RegionErase" => Cursors.UpArrow,
            _ => Cursors.Arrow
        };
    }
}
