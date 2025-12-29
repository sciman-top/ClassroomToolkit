using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Media.Animation;
using MediaColor = System.Windows.Media.Color;
using WpfCursor = System.Windows.Input.Cursor;
using WpfPoint = System.Windows.Point;
using WpfPen = System.Windows.Media.Pen;
using IOPath = System.IO.Path;

namespace ClassroomToolkit.App.Utilities;

/// <summary>
/// 自定义光标生成器 - 为画笔工具创建形象美观的光标
/// </summary>
public static class CustomCursors
{
    private static WpfCursor? _brushCursor;
    private static MediaColor _currentBrushColor = Colors.Red;
    private static WpfCursor? _eraserCursor;
    private static WpfCursor? _regionEraseCursor;

    /// <summary>
    /// 获取指定颜色的画笔光标
    /// </summary>
    public static WpfCursor GetBrushCursor(MediaColor color)
    {
        // 如果颜色变化或缓存不存在，重新创建
        if (_brushCursor == null || !AreColorsEqual(_currentBrushColor, color))
        {
            _currentBrushColor = color;
            _brushCursor = CreateColoredBrushCursor(color);
        }
        return _brushCursor;
    }

    /// <summary>
    /// 橡皮擦光标
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
    /// 框选擦除光标
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

    /// <summary>
    /// 创建带颜色的画笔光标 - 圆形设计，直观显示颜色
    /// </summary>
    private static WpfCursor CreateColoredBrushCursor(MediaColor color)
    {
        const int cursorSize = 32;
        const int centerX = 16;
        const int centerY = 16;
        const int outerRadius = 12;
        const int innerRadius = 8;

        // 创建画笔形状的 DrawingVisual
        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 外圈白色描边（增强对比度）
            var whiteBrush = new SolidColorBrush(Colors.White);
            whiteBrush.Freeze();
            var whitePen = new WpfPen(whiteBrush, 3);
            whitePen.Freeze();

            // 外圈（带白色描边）
            var outerCircle = new EllipseGeometry(new WpfPoint(centerX, centerY), outerRadius, outerRadius);
            context.DrawGeometry(whiteBrush, whitePen, outerCircle);

            // 内圈彩色圆（实际画笔颜色）
            var brushColor = MediaColor.FromRgb(color.R, color.G, color.B);
            var colorBrush = new SolidColorBrush(brushColor);
            colorBrush.Freeze();

            var innerCircle = new EllipseGeometry(new WpfPoint(centerX, centerY), innerRadius, innerRadius);
            context.DrawGeometry(colorBrush, null, innerCircle);

            // 中心十字准星（黑色细线）
            var crosshairPen = new WpfPen(System.Windows.Media.Brushes.Black, 1);
            crosshairPen.Freeze();

            // 横线
            context.DrawLine(crosshairPen,
                new WpfPoint(centerX - 3, centerY),
                new WpfPoint(centerX + 3, centerY));
            // 竖线
            context.DrawLine(crosshairPen,
                new WpfPoint(centerX, centerY - 3),
                new WpfPoint(centerX, centerY + 3));

            // 外圈黑色描边（最外层）
            var blackPen = new WpfPen(System.Windows.Media.Brushes.Black, 1);
            blackPen.Freeze();
            context.DrawGeometry(null, blackPen, outerCircle);
        }

        return CreateCursorFromVisual(drawingVisual, cursorSize, centerX, centerY,
            $"brush_{color.R}_{color.G}_{color.B}");
    }

    /// <summary>
    /// 从位图创建 .cur 光标文件
    /// </summary>
    private static void CreateCursorFile(BitmapSource bitmap, int hotSpotX, int hotSpotY, string outputPath)
    {
        // 转换为 32bpp RGBA 格式
        var formattedBitmap = new FormatConvertedBitmap();
        formattedBitmap.BeginInit();
        formattedBitmap.Source = bitmap;
        formattedBitmap.DestinationFormat = PixelFormats.Bgra32;
        formattedBitmap.EndInit();

        int width = formattedBitmap.PixelWidth;
        int height = formattedBitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];
        formattedBitmap.CopyPixels(pixels, stride, 0);

        // 创建 AND 掩码（单色位图，用于光标透明度）
        int andStride = (width + 7) / 8;
        byte[] andMask = new byte[height * andStride];

        // 创建 XOR 掩码（彩色位图）
        byte[] xorMask = new byte[height * stride];
        Array.Copy(pixels, xorMask, pixels.Length);

        // 光标文件头
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var writer = new BinaryWriter(fs);

        // ICONDIR 结构
        writer.Write((short)0);           // 保留
        writer.Write((short)2);           // 资源类型: 2 = 光标
        writer.Write((short)1);           // 图像数量

        // ICONDIRENTRY 结构
        writer.Write((byte)width);        // 宽度
        writer.Write((byte)height);       // 高度
        writer.Write((byte)0);            // 颜色数
        writer.Write((byte)0);            // 保留
        writer.Write((short)hotSpotX);    // 热点 X
        writer.Write((short)hotSpotY);    // 热点 Y

        // 计算数据大小
        int bitmapHeaderSize = 40; // BITMAPINFOHEADER
        int colorTableSize = 0;    // 没有 RGB 颜色表（使用 PNG）
        int imageDataSize = andMask.Length + xorMask.Length;
        int dataEntrySize = bitmapHeaderSize + colorTableSize + imageDataSize;

        writer.Write((int)dataEntrySize); // 数据大小
        writer.Write((int)22);            // 数据偏移（ICONDIR + ICONDIRENTRY）

        // BITMAPINFOHEADER
        writer.Write((int)bitmapHeaderSize); // 结构大小
        writer.Write((int)width);             // 宽度
        writer.Write((int)height * 2);        // 高度（XOR + AND）
        writer.Write((short)1);               // 平面数
        writer.Write((short)32);              // 每像素位数
        writer.Write((int)0);                 // 压缩方式
        writer.Write((int)0);                 // 图像大小（未压缩 = 0）
        writer.Write((int)0);                 // 水平分辨率
        writer.Write((int)0);                 // 垂直分辨率
        writer.Write((int)0);                 // 使用的颜色数
        writer.Write((int)0);                 // 重要的颜色数

        // 写入 XOR 掩码（自下而上）
        for (int y = height - 1; y >= 0; y--)
        {
            writer.Write(xorMask, y * stride, stride);
        }

        // 写入 AND 掩码（自下而上）
        for (int y = height - 1; y >= 0; y--)
        {
            writer.Write(andMask, y * andStride, andStride);
        }
    }

    /// <summary>
    /// 创建橡皮擦光标 - 方形设计，带斜线纹理
    /// </summary>
    private static WpfCursor CreateEraserCursor()
    {
        const int cursorSize = 32;
        const int boxSize = 20;
        const int offset = (cursorSize - boxSize) / 2;

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 白色背景方块
            var whiteBrush = new SolidColorBrush(Colors.White);
            whiteBrush.Freeze();

            // 方形边框
            var blackPen = new WpfPen(System.Windows.Media.Brushes.Black, 2);
            blackPen.Freeze();

            var rect = new Rect(offset, offset, boxSize, boxSize);
            var rectGeom = new RectangleGeometry(rect);
            context.DrawGeometry(whiteBrush, blackPen, rectGeom);

            // 绘制斜线纹理（表示擦除）
            var dashPen = new WpfPen(new SolidColorBrush(MediaColor.FromRgb(150, 150, 150)), 1.5);
            dashPen.Freeze();

            // 斜线从左上到右下
            for (int i = 0; i < boxSize; i += 4)
            {
                context.DrawLine(dashPen,
                    new WpfPoint(offset + i, offset + boxSize),
                    new WpfPoint(offset + boxSize, offset + i));
            }

            // 中心点
            var centerBrush = new SolidColorBrush(MediaColor.FromRgb(100, 100, 100));
            centerBrush.Freeze();
            var centerRect = new Rect(new WpfPoint(cursorSize / 2 - 1, cursorSize / 2 - 1), new WpfSize(2, 2));
            context.DrawRectangle(centerBrush, null, centerRect);
        }

        return CreateCursorFromVisual(drawingVisual, cursorSize, cursorSize / 2, cursorSize / 2, "eraser");
    }

    /// <summary>
    /// 创建框选擦除光标 - 虚线矩形框设计
    /// </summary>
    private static WpfCursor CreateRegionEraseCursor()
    {
        const int cursorSize = 32;
        const int boxSize = 24;
        const int offset = (cursorSize - boxSize) / 2;

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 外框虚线矩形
            var dashedPen = new WpfPen(System.Windows.Media.Brushes.Black, 2);
            dashedPen.DashStyle = DashStyles.Dash;
            dashedPen.Freeze();

            var rect = new Rect(offset, offset, boxSize, boxSize);
            context.DrawRectangle(null, dashedPen, rect);

            // 四个角的实线强调
            var solidPen = new WpfPen(System.Windows.Media.Brushes.Black, 2.5);
            solidPen.Freeze();

            const int cornerSize = 6;
            // 左上角
            context.DrawLine(solidPen,
                new WpfPoint(offset, offset + cornerSize),
                new WpfPoint(offset, offset));
            context.DrawLine(solidPen,
                new WpfPoint(offset, offset),
                new WpfPoint(offset + cornerSize, offset));

            // 右上角
            context.DrawLine(solidPen,
                new WpfPoint(offset + boxSize - cornerSize, offset),
                new WpfPoint(offset + boxSize, offset));
            context.DrawLine(solidPen,
                new WpfPoint(offset + boxSize, offset),
                new WpfPoint(offset + boxSize, offset + cornerSize));

            // 左下角
            context.DrawLine(solidPen,
                new WpfPoint(offset, offset + boxSize - cornerSize),
                new WpfPoint(offset, offset + boxSize));
            context.DrawLine(solidPen,
                new WpfPoint(offset, offset + boxSize),
                new WpfPoint(offset + cornerSize, offset + boxSize));

            // 右下角
            context.DrawLine(solidPen,
                new WpfPoint(offset + boxSize - cornerSize, offset + boxSize),
                new WpfPoint(offset + boxSize, offset + boxSize));
            context.DrawLine(solidPen,
                new WpfPoint(offset + boxSize, offset + boxSize),
                new WpfPoint(offset + boxSize, offset + boxSize - cornerSize));

            // 中心十字
            var crossPen = new WpfPen(System.Windows.Media.Brushes.Red, 2);
            crossPen.Freeze();

            context.DrawLine(crossPen,
                new WpfPoint(cursorSize / 2 - 4, cursorSize / 2),
                new WpfPoint(cursorSize / 2 + 4, cursorSize / 2));
            context.DrawLine(crossPen,
                new WpfPoint(cursorSize / 2, cursorSize / 2 - 4),
                new WpfPoint(cursorSize / 2, cursorSize / 2 + 4));
        }

        return CreateCursorFromVisual(drawingVisual, cursorSize, cursorSize / 2, cursorSize / 2, "region_erase");
    }

    /// <summary>
    /// 从 DrawingVisual 创建光标
    /// </summary>
    private static WpfCursor CreateCursorFromVisual(DrawingVisual visual, int size, int hotSpotX, int hotSpotY, string name)
    {
        // 渲染到位图
        var renderBitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        renderBitmap.Render(visual);

        // 创建临时 .cur 文件
        var cursorPath = IOPath.Combine(
            IOPath.GetTempPath(),
            $"{name}_{Guid.NewGuid():N}.cur");

        CreateCursorFile(renderBitmap, hotSpotX, hotSpotY, cursorPath);

        // 加载光标
        return new WpfCursor(cursorPath);
    }

    /// <summary>
    /// 比较两个颜色是否相等
    /// </summary>
    private static bool AreColorsEqual(MediaColor a, MediaColor b)
    {
        return a.A == b.A && a.R == b.R && a.G == b.G && a.B == b.B;
    }
}
