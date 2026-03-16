using System;
using System.Collections.Generic;
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
    private static readonly Dictionary<int, WpfCursor> EraserCursorCache = new();
    private static WpfCursor? _regionEraseCursor;
    private static readonly List<string> TempCursorFiles = new();
    private static readonly object TempCursorLock = new();

    static CustomCursors()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CleanupTempCursorFiles();
    }

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
    /// 橡皮擦光标（兼容旧调用）
    /// </summary>
    public static WpfCursor Eraser
    {
        get => GetEraserCursor(24.0);
    }

    /// <summary>
    /// 橡皮擦光标（包含可擦除范围圈）
    /// </summary>
    public static WpfCursor GetEraserCursor(double eraserSize)
    {
        int bucket = (int)Math.Round(Math.Max(4.0, Math.Min(96.0, eraserSize)));
        if (!EraserCursorCache.TryGetValue(bucket, out var cursor))
        {
            cursor = CreateEraserCursor(bucket);
            EraserCursorCache[bucket] = cursor;
        }
        return cursor;
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
    /// 创建橡皮擦光标 - 使用矢量图标渲染，更加形象直观
    /// </summary>
    private static WpfCursor CreateEraserCursor(double eraserSize)
    {
        const int cursorSize = 96;
        const int iconSize = 24;
        const int offset = (cursorSize - iconSize) / 2;
        var center = new WpfPoint(cursorSize / 2.0, cursorSize / 2.0);
        var radius = Math.Max(2.0, eraserSize * 0.5);

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            // 先画擦除范围圈（双层描边，保证各种背景下可见）
            var outerRangePen = new WpfPen(new SolidColorBrush(MediaColor.FromArgb(220, 0, 0, 0)), 2.0);
            outerRangePen.Freeze();
            var innerRangePen = new WpfPen(new SolidColorBrush(MediaColor.FromArgb(230, 255, 255, 255)), 1.0);
            innerRangePen.Freeze();
            var rangeGeometry = new EllipseGeometry(center, radius, radius);
            context.DrawGeometry(null, outerRangePen, rangeGeometry);
            context.DrawGeometry(null, innerRangePen, rangeGeometry);

            // 1. 从 XAML 资源加载橡皮擦图标几何形状（避免 Geometry.Parse 的区域设置问题）
            var resourceGeometry = (Geometry)System.Windows.Application.Current.FindResource("Icon_Eraser");
            var eraserGeometry = resourceGeometry.Clone();
            
            // 2. 变换：居中显示
            var transform = new TranslateTransform(offset, offset);
            eraserGeometry.Transform = transform;

            // 3. 绘制阴影（增强立体感）
            var shadowBrush = new SolidColorBrush(MediaColor.FromArgb(60, 0, 0, 0));
            shadowBrush.Freeze();
            var shadowPen = new WpfPen(shadowBrush, 2);
            shadowPen.Freeze();
            var shadowTransform = new TranslateTransform(offset + 1, offset + 1);
            var shadowGeometry = eraserGeometry.Clone();
            shadowGeometry.Transform = shadowTransform;
            context.DrawGeometry(shadowBrush, null, shadowGeometry);

            // 4. 定义画笔
            // 主体填充：白色 (高对比度)
            var fillBrush = new SolidColorBrush(Colors.White);
            fillBrush.Freeze();
            
            // 描边：深灰色 (清晰轮廓)
            var outlinePen = new WpfPen(new SolidColorBrush(MediaColor.FromRgb(30, 30, 30)), 1.0);
            outlinePen.LineJoin = PenLineJoin.Round;
            outlinePen.Freeze();

            // 5. 绘制主体
            context.DrawGeometry(fillBrush, outlinePen, eraserGeometry);

            // 6. 绘制装饰/细节 (橡皮擦套筒部分 - 蓝色)
            // 通过裁剪或重绘部分区域来模拟套筒颜色
            // 简单的做法是：绘制一个覆盖在中间的蓝色带子，或者直接用几何路径的第二部分
            // 观察 geometry, M4.22,15.58... 是下面的部分(橡皮头), M16.24... 是整体?
            // Material Icon path 通常是一个整体。为了简单且好看，我们在中间加一个装饰带。
            
            var bandBrush = new SolidColorBrush(MediaColor.FromRgb(59, 130, 246)); // Electric Blue
            bandBrush.Freeze();
            
            // 创建一个裁剪区域来绘制蓝色的"手柄"部分 (右上部分)
            // 简单的近似：用一个旋转矩形覆盖右上半部分
            context.PushClip(eraserGeometry);
            
            // 绘制蓝色矩形覆盖上半部分 (模拟手柄)
            var bandRect = new Rect(offset + 8, offset - 5, 20, 20);
            context.PushTransform(new RotateTransform(45, offset + 12, offset + 12));
            context.DrawRectangle(bandBrush, null, bandRect);
            context.Pop(); // Pop Rotate
            
            context.Pop(); // Pop Clip

            // 重新描边以确保轮廓清晰（因为Clip可能切断了描边）
            context.DrawGeometry(null, outlinePen, eraserGeometry);
        }

        // 热点设为中心
        return CreateCursorFromVisual(drawingVisual, cursorSize, cursorSize / 2, cursorSize / 2, "icon_eraser");
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
        TrackTempCursorFile(cursorPath);

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

    private static void TrackTempCursorFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }
        lock (TempCursorLock)
        {
            TempCursorFiles.Add(path);
        }
    }

    private static void CleanupTempCursorFiles()
    {
        List<string> files;
        lock (TempCursorLock)
        {
            files = new List<string>(TempCursorFiles);
            TempCursorFiles.Clear();
        }
        foreach (var path in files)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
            {
                // Ignore cleanup failures.
            }
        }
    }
}

