using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkStrokeRendererCompositeTests
{
    [Fact]
    public void RenderPage_ShouldGracefullyHandleNullCollectionsAndInvalidColors()
    {
        var renderer = new InkStrokeRenderer();
        var geometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(40, 40, 120, 120)));
        var invalidColorPage = new InkPageData
        {
            PageIndex = 1,
            Strokes = new List<InkStrokeData>
            {
                new()
                {
                    GeometryPath = geometryPath,
                    ColorHex = "not-a-wpf-color",
                    Ribbons = null!,
                    Blooms = null!
                }
            }
        };
        var nullCollectionPage = new InkPageData
        {
            PageIndex = 2,
            Strokes = null!
        };

        Action render = () =>
        {
            renderer.RenderPage(nullCollectionPage, 220, 220, 96, 96);
            renderer.RenderPage(invalidColorPage, 220, 220, 96, 96);
        };

        render.Should().NotThrow();
    }

    [Fact]
    public void RenderPage_CalligraphyStroke_ShouldApplyPersistedBloomOverlay()
    {
        var withoutOverlays = RenderCalligraphyStroke(includeOverlays: false, mode: CalligraphyRenderMode.Ink, strokeOpacity: 140);
        var withOverlays = RenderCalligraphyStroke(includeOverlays: true, mode: CalligraphyRenderMode.Ink, strokeOpacity: 140);

        ReadPixel(withoutOverlays, 90, 90).Should().NotEqual(ReadPixel(withOverlays, 90, 90));
        ReadPixel(withoutOverlays, 120, 120).Should().NotEqual(ReadPixel(withOverlays, 120, 120));
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

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void RenderPage_CalligraphyStroke_ShouldFailClosedForNonFiniteInkFlow(double inkFlow)
    {
        var renderer = new InkStrokeRenderer();
        var geometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(40, 40, 120, 120)));
        var page = new InkPageData
        {
            PageIndex = 1,
            Strokes = new List<InkStrokeData>
            {
                new()
                {
                    Type = InkStrokeType.Brush,
                    BrushStyle = PaintBrushStyle.Calligraphy,
                    GeometryPath = geometryPath,
                    ColorHex = "#000000",
                    BrushSize = 16.0,
                    CalligraphyRenderMode = CalligraphyRenderMode.Ink,
                    CalligraphyInkBloomEnabled = false,
                    CalligraphySealEnabled = false,
                    CalligraphyOverlayOpacityThreshold = 0,
                    InkFlow = inkFlow,
                    StrokeDirectionX = 1.0
                }
            }
        };

        Action render = () => renderer.RenderPage(page, 220, 220, 96, 96);
        render.Should().NotThrow();
    }

    [Fact]
    public void RenderPage_CalligraphyStroke_ShouldUseStableFallbackForNonFiniteInkFlow()
    {
        var invalid = RenderCalligraphyStroke(
            includeOverlays: true,
            mode: CalligraphyRenderMode.Ink,
            inkFlow: double.NaN);
        var fallback = RenderCalligraphyStroke(
            includeOverlays: true,
            mode: CalligraphyRenderMode.Ink,
            inkFlow: 0.5);

        foreach (var probe in new[] { (X: 52, Y: 52), (X: 96, Y: 96), (X: 148, Y: 112) })
        {
            var invalidPixel = ReadPixel(invalid, probe.X, probe.Y);
            var fallbackPixel = ReadPixel(fallback, probe.X, probe.Y);
            invalidPixel.Should().Equal(fallbackPixel);
        }

        ReadPixels(invalid).Should().Equal(ReadPixels(fallback));
    }

    private static RenderTargetBitmap RenderCalligraphyStroke(
        bool includeOverlays,
        CalligraphyRenderMode mode,
        byte strokeOpacity = 255,
        double inkFlow = 0.72)
    {
        var renderer = new InkStrokeRenderer();
        var geometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(40, 40, 120, 120)));
        var stroke = new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = PaintBrushStyle.Calligraphy,
            GeometryPath = geometryPath,
            ColorHex = "#000000",
            Opacity = strokeOpacity,
            BrushSize = 16.0,
            MaskSeed = 12345,
            CalligraphyRenderMode = mode,
            CalligraphySealEnabled = false,
            CalligraphyInkBloomEnabled = includeOverlays,
            CalligraphyOverlayOpacityThreshold = 0,
            InkFlow = inkFlow,
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

    private static byte[] ReadPixels(BitmapSource bitmap)
    {
        int stride = bitmap.PixelWidth * 4;
        var pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
