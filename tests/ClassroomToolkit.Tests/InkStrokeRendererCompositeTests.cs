using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkStrokeRendererCompositeTests
{
    [Fact]
    public void RenderPage_CalligraphyStroke_ShouldNotDarkenWhenRibbonOverlaysOverlap()
    {
        var withoutOverlays = RenderCalligraphyStroke(includeOverlays: false, mode: CalligraphyRenderMode.Clarity);
        var withOverlays = RenderCalligraphyStroke(includeOverlays: true, mode: CalligraphyRenderMode.Clarity);

        ReadPixel(withoutOverlays, 90, 90).Should().Equal(ReadPixel(withOverlays, 90, 90));
        ReadPixel(withoutOverlays, 120, 120).Should().Equal(ReadPixel(withOverlays, 120, 120));
    }

    [Fact]
    public void RenderPage_CalligraphyInkMode_ShouldRenderDeterministically()
    {
        var inkA = RenderCalligraphyStroke(includeOverlays: true, mode: CalligraphyRenderMode.Ink);
        var inkB = RenderCalligraphyStroke(includeOverlays: true, mode: CalligraphyRenderMode.Ink);

        var probes = new[]
        {
            (X: 52, Y: 52),
            (X: 96, Y: 96),
            (X: 148, Y: 112)
        };
        bool allEqual = probes.All(p => ReadPixel(inkA, p.X, p.Y).SequenceEqual(ReadPixel(inkB, p.X, p.Y)));
        allEqual.Should().BeTrue();
        ReadPixel(inkA, 96, 96)[3].Should().BeGreaterThan((byte)170);
    }

    private static RenderTargetBitmap RenderCalligraphyStroke(bool includeOverlays, CalligraphyRenderMode mode)
    {
        var renderer = new InkStrokeRenderer();
        var geometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(40, 40, 120, 120)));
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = PaintBrushStyle.Calligraphy,
            GeometryPath = geometryPath,
            ColorHex = "#000000",
            Opacity = 255,
            BrushSize = 16.0,
            MaskSeed = 12345,
            CalligraphyRenderMode = mode,
            CalligraphySealEnabled = false,
            CalligraphyInkBloomEnabled = includeOverlays,
            CalligraphyOverlayOpacityThreshold = 0,
            InkFlow = 0.72,
            StrokeDirectionX = 1.0,
            StrokeDirectionY = 0.0
        };

        if (includeOverlays)
        {
            stroke.Ribbons.Add(new InkRibbonData
            {
                GeometryPath = geometryPath,
                Opacity = 0.32,
                RibbonT = 0.0
            });
            stroke.Ribbons.Add(new InkRibbonData
            {
                GeometryPath = geometryPath,
                Opacity = 0.18,
                RibbonT = 1.0
            });
            stroke.Blooms.Add(new InkBloomData
            {
                GeometryPath = geometryPath,
                Opacity = 0.24
            });
        }

        var page = new InkPageData
        {
            PageIndex = 1,
            Strokes = new List<InkStrokeData> { stroke }
        };

        return renderer.RenderPage(page, 220, 220, 96, 96);
    }

    private static byte[] ReadPixel(BitmapSource bitmap, int x, int y)
    {
        var pixel = new byte[4];
        bitmap.CopyPixels(new Int32Rect(x, y, 1, 1), pixel, 4, 0);
        return pixel;
    }
}
