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
using WpfSize = System.Windows.Size;
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
    /// 创建橡皮擦光标 - 立体橡皮擦设计，带擦除效果
    /// </summary>
    private static WpfCursor CreateEraserCursor()
    {
        const int cursorSize = 32;
        const int eraserWidth = 18;
        const int eraserHeight = 22;
        const int offsetX = (cursorSize - eraserWidth) / 2;
        const int offsetY = (cursorSize - eraserHeight) / 2;

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 橡皮擦主体 - 粉红色渐变
            var eraserBrush = new LinearGradientBrush(
                MediaColor.FromRgb(255, 182, 193), // 浅粉色
                MediaColor.FromRgb(255, 105, 180), // 深粉色
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1));
            eraserBrush.Freeze();

            // 金属夹子 - 银色渐变
            var clipBrush = new LinearGradientBrush(
                MediaColor.FromRgb(192, 192, 192), // 浅银色
                MediaColor.FromRgb(128, 128, 128), // 深银色
                new System.Windows.Point(0, 0),
                new System.Windows.Point(0, 1));
            clipBrush.Freeze();

            // 橡皮擦主体形状（圆角矩形）
            var eraserRect = new Rect(offsetX, offsetY + 4, eraserWidth, eraserHeight - 4);
            var eraserGeom = new RectangleGeometry(eraserRect, 3, 3);
            context.DrawGeometry(eraserBrush, new WpfPen(System.Windows.Media.Brushes.Gray, 1), eraserGeom);

            // 金属夹子
            var clipRect = new Rect(offsetX + 2, offsetY - 2, eraserWidth - 4, 8);
            var clipGeom = new RectangleGeometry(clipRect, 2, 2);
            context.DrawGeometry(clipBrush, new WpfPen(System.Windows.Media.Brushes.DarkGray, 1), clipGeom);

            // 擦除痕迹效果（半透明白色）
            var eraseEffectBrush = new SolidColorBrush(MediaColor.FromArgb(100, 255, 255, 255));
            eraseEffectBrush.Freeze();
            
            // 擦除线条
            var erasePen = new WpfPen(eraseEffectBrush, 2);
            erasePen.Freeze();
            
            // 模拟擦除的轨迹
            context.DrawLine(erasePen,
                new WpfPoint(offsetX - 2, offsetY + eraserHeight - 2),
                new WpfPoint(offsetX + eraserWidth + 2, offsetY + eraserHeight + 4));
            
            context.DrawLine(erasePen,
                new WpfPoint(offsetX - 1, offsetY + eraserHeight),
                new WpfPoint(offsetX + eraserWidth + 1, offsetY + eraserHeight + 2));

            // 中心十字准星（精确定位）
            var crossPen = new WpfPen(System.Windows.Media.Brushes.Black, 1);
            crossPen.Freeze();
            
            int centerX = cursorSize / 2;
            int centerY = cursorSize / 2;
            
            context.DrawLine(crossPen,
                new WpfPoint(centerX - 3, centerY),
                new WpfPoint(centerX + 3, centerY));
            context.DrawLine(crossPen,
                new WpfPoint(centerX, centerY - 3),
                new WpfPoint(centerX, centerY + 3));
        }

        return CreateCursorFromVisual(drawingVisual, cursorSize, cursorSize / 2, cursorSize / 2, "eraser");
    }

    /// <summary>
    /// 创建框选擦除光标 - 现代化选择框设计，带动态效果
    /// </summary>
    private static WpfCursor CreateRegionEraseCursor()
    {
        const int cursorSize = 32;
        const int selectionSize = 26;
        const int offset = (cursorSize - selectionSize) / 2;

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 选择框主体 - 蓝色渐变边框
            var borderBrush = new LinearGradientBrush(
                MediaColor.FromRgb(70, 130, 255),   // 亮蓝色
                MediaColor.FromRgb(0, 90, 200),     // 深蓝色
                new System.Windows.Point(0, 0),
                new System.Windows.Point(1, 1));
            borderBrush.Freeze();

            // 填充背景 - 半透明蓝色
            var fillBrush = new SolidColorBrush(MediaColor.FromArgb(30, 70, 130, 255));
            fillBrush.Freeze();

            // 主选择框
            var mainRect = new Rect(offset, offset, selectionSize, selectionSize);
            var mainGeom = new RectangleGeometry(mainRect);
            context.DrawGeometry(fillBrush, new WpfPen(borderBrush, 2), mainGeom);

            // 四个角的抓取点 - 更明显的白色方块
            var grabBrush = new SolidColorBrush(Colors.White);
            grabBrush.Freeze();
            var grabPen = new WpfPen(System.Windows.Media.Brushes.DarkBlue, 1.5);
            grabPen.Freeze();

            const int grabSize = 4;
            // 左上角
            var tlGrab = new Rect(offset - 1, offset - 1, grabSize, grabSize);
            context.DrawRectangle(grabBrush, grabPen, tlGrab);

            // 右上角
            var trGrab = new Rect(offset + selectionSize - grabSize + 1, offset - 1, grabSize, grabSize);
            context.DrawRectangle(grabBrush, grabPen, trGrab);

            // 左下角
            var blGrab = new Rect(offset - 1, offset + selectionSize - grabSize + 1, grabSize, grabSize);
            context.DrawRectangle(grabBrush, grabPen, blGrab);

            // 右下角
            var brGrab = new Rect(offset + selectionSize - grabSize + 1, offset + selectionSize - grabSize + 1, grabSize, grabSize);
            context.DrawRectangle(grabBrush, grabPen, brGrab);

            // 中心删除图标 - 红色X标记
            var deletePen = new WpfPen(System.Windows.Media.Brushes.Red, 3)
            {
                StartLineCap = PenLineCap.Round,
                EndLineCap = PenLineCap.Round
            };
            deletePen.Freeze();

            int centerX = cursorSize / 2;
            int centerY = cursorSize / 2;
            int xSize = 6;

            // X标记的斜线
            context.DrawLine(deletePen,
                new WpfPoint(centerX - xSize, centerY - xSize),
                new WpfPoint(centerX + xSize, centerY + xSize));
            context.DrawLine(deletePen,
                new WpfPoint(centerX - xSize, centerY + xSize),
                new WpfPoint(centerX + xSize, centerY - xSize));

            // 中心十字准星（精确定位）
            var crossPen = new WpfPen(System.Windows.Media.Brushes.Black, 1);
            crossPen.Freeze();
            
            context.DrawLine(crossPen,
                new WpfPoint(centerX - 2, centerY),
                new WpfPoint(centerX + 2, centerY));
            context.DrawLine(crossPen,
                new WpfPoint(centerX, centerY - 2),
                new WpfPoint(centerX, centerY + 2));

            // 添加选择效果线条（虚线）
            var dashPen = new WpfPen(System.Windows.Media.Brushes.LightBlue, 1);
            dashPen.DashStyle = DashStyles.Dot;
            dashPen.Freeze();

            // 内部虚线框
            var innerRect = new Rect(offset + 4, offset + 4, selectionSize - 8, selectionSize - 8);
            context.DrawRectangle(null, dashPen, innerRect);
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
