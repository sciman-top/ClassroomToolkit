using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InkExportCoordinateInvariantTests
{
    private static readonly MethodInfo AdaptStrokesForBackgroundMethod =
        typeof(InkExportService).GetMethod(
            "AdaptStrokesForBackground",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException(
            $"{nameof(InkExportService)}.AdaptStrokesForBackground");

    [Fact]
    public void AdaptStrokesForBackground_ShouldPreserveGeometryAfterScaleRoundTrip()
    {
        var original = BuildStroke(
            geometry: new RectangleGeometry(new Rect(12, 18, 36, 24)),
            brushSize: 10,
            referenceWidth: 100,
            referenceHeight: 80);

        var scaledBackground = CreateBitmap(width: 200, height: 160, dpi: 96);
        var scaled = Adapt(new List<InkStrokeData> { original }, scaledBackground, fallbackScale: 1.0);
        scaled.Should().HaveCount(1);
        scaled[0].ReferenceWidth.Should().BeApproximately(200, 0.001);
        scaled[0].ReferenceHeight.Should().BeApproximately(160, 0.001);
        scaled[0].BrushSize.Should().BeApproximately(20, 0.001);

        var restoredBackground = CreateBitmap(width: 100, height: 80, dpi: 96);
        var restored = Adapt(scaled, restoredBackground, fallbackScale: 1.0);
        restored.Should().HaveCount(1);
        restored[0].BrushSize.Should().BeApproximately(original.BrushSize, 0.001);

        var originalBounds = DeserializeBounds(original.GeometryPath);
        var restoredBounds = DeserializeBounds(restored[0].GeometryPath);
        restoredBounds.X.Should().BeApproximately(originalBounds.X, 0.02);
        restoredBounds.Y.Should().BeApproximately(originalBounds.Y, 0.02);
        restoredBounds.Width.Should().BeApproximately(originalBounds.Width, 0.02);
        restoredBounds.Height.Should().BeApproximately(originalBounds.Height, 0.02);
    }

    [Fact]
    public void AdaptStrokesForBackground_ShouldScaleRibbonAndBloomGeometryWithMainStroke()
    {
        var stroke = BuildStroke(
            geometry: new RectangleGeometry(new Rect(10, 10, 40, 20)),
            brushSize: 6,
            referenceWidth: 100,
            referenceHeight: 100);
        stroke.Ribbons.Add(new InkRibbonData
        {
            GeometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(12, 11, 36, 18))),
            Opacity = 0.3,
            RibbonT = 0.5
        });
        stroke.Blooms.Add(new InkBloomData
        {
            GeometryPath = InkGeometrySerializer.Serialize(new RectangleGeometry(new Rect(8, 8, 44, 24))),
            Opacity = 0.2
        });

        var target = CreateBitmap(width: 300, height: 150, dpi: 96);
        var adapted = Adapt(new List<InkStrokeData> { stroke }, target, fallbackScale: 1.0);
        adapted.Should().HaveCount(1);
        adapted[0].Ribbons.Should().HaveCount(1);
        adapted[0].Blooms.Should().HaveCount(1);

        var mainBounds = DeserializeBounds(adapted[0].GeometryPath);
        var ribbonBounds = DeserializeBounds(adapted[0].Ribbons[0].GeometryPath);
        var bloomBounds = DeserializeBounds(adapted[0].Blooms[0].GeometryPath);

        mainBounds.Width.Should().BeApproximately(120, 0.05);
        mainBounds.Height.Should().BeApproximately(30, 0.05);
        ribbonBounds.Width.Should().BeApproximately(108, 0.05);
        ribbonBounds.Height.Should().BeApproximately(27, 0.05);
        bloomBounds.Width.Should().BeApproximately(132, 0.05);
        bloomBounds.Height.Should().BeApproximately(36, 0.05);
    }

    [Fact]
    public void AdaptStrokesForBackground_ShouldStayGeometryStable_WhenOnlyDpiChangesButDipSizeSame()
    {
        var stroke = BuildStroke(
            geometry: new RectangleGeometry(new Rect(20, 12, 64, 28)),
            brushSize: 9,
            referenceWidth: 200,
            referenceHeight: 100);

        var dpi96 = CreateBitmap(width: 200, height: 100, dpi: 96);
        var dpi144 = CreateBitmap(width: 300, height: 150, dpi: 144);

        var from96 = Adapt(new List<InkStrokeData> { stroke }, dpi96, fallbackScale: 1.0);
        var from144 = Adapt(new List<InkStrokeData> { stroke }, dpi144, fallbackScale: 1.0);
        from96.Should().HaveCount(1);
        from144.Should().HaveCount(1);

        var b96 = DeserializeBounds(from96[0].GeometryPath);
        var b144 = DeserializeBounds(from144[0].GeometryPath);
        b144.X.Should().BeApproximately(b96.X, 0.02);
        b144.Y.Should().BeApproximately(b96.Y, 0.02);
        b144.Width.Should().BeApproximately(b96.Width, 0.02);
        b144.Height.Should().BeApproximately(b96.Height, 0.02);
        from144[0].BrushSize.Should().BeApproximately(from96[0].BrushSize, 0.001);
    }

    [Fact]
    public void AdaptStrokesForBackground_ShouldUseFallbackScale_WhenReferenceSizeMissing()
    {
        var stroke = BuildStroke(
            geometry: new RectangleGeometry(new Rect(5, 6, 10, 12)),
            brushSize: 4,
            referenceWidth: 0,
            referenceHeight: 0);

        var target = CreateBitmap(width: 400, height: 200, dpi: 96);
        var adapted = Adapt(new List<InkStrokeData> { stroke }, target, fallbackScale: 1.5);
        adapted.Should().HaveCount(1);

        var bounds = DeserializeBounds(adapted[0].GeometryPath);
        bounds.X.Should().BeApproximately(7.5, 0.02);
        bounds.Y.Should().BeApproximately(9.0, 0.02);
        bounds.Width.Should().BeApproximately(15.0, 0.02);
        bounds.Height.Should().BeApproximately(18.0, 0.02);
        adapted[0].BrushSize.Should().BeApproximately(6.0, 0.001);
    }

    [Fact]
    public void AdaptStrokesForBackground_ShouldPreserveGeometryAfterNonUniformScaleRoundTrip()
    {
        var stroke = BuildStroke(
            geometry: new RectangleGeometry(new Rect(11, 17, 23, 31)),
            brushSize: 7.5,
            referenceWidth: 100,
            referenceHeight: 100);

        var stretched = CreateBitmap(width: 250, height: 400, dpi: 96);
        var stretchedResult = Adapt(new List<InkStrokeData> { stroke }, stretched, fallbackScale: 1.0);
        stretchedResult.Should().HaveCount(1);

        var backToSquare = CreateBitmap(width: 100, height: 100, dpi: 96);
        var restored = Adapt(stretchedResult, backToSquare, fallbackScale: 1.0);
        restored.Should().HaveCount(1);

        var originalBounds = DeserializeBounds(stroke.GeometryPath);
        var restoredBounds = DeserializeBounds(restored[0].GeometryPath);

        restoredBounds.X.Should().BeApproximately(originalBounds.X, 0.03);
        restoredBounds.Y.Should().BeApproximately(originalBounds.Y, 0.03);
        restoredBounds.Width.Should().BeApproximately(originalBounds.Width, 0.03);
        restoredBounds.Height.Should().BeApproximately(originalBounds.Height, 0.03);
        restored[0].BrushSize.Should().BeApproximately(stroke.BrushSize, 0.002);
    }

    private static InkStrokeData BuildStroke(
        Geometry geometry,
        double brushSize,
        double referenceWidth,
        double referenceHeight)
    {
        return new InkStrokeData
        {
            Type = InkStrokeType.Brush,
            BrushStyle = PaintBrushStyle.Calligraphy,
            ColorHex = "#FF0000",
            Opacity = 255,
            BrushSize = brushSize,
            GeometryPath = InkGeometrySerializer.Serialize(geometry),
            ReferenceWidth = referenceWidth,
            ReferenceHeight = referenceHeight
        };
    }

    private static Rect DeserializeBounds(string path)
    {
        var geometry = InkGeometrySerializer.Deserialize(path);
        geometry.Should().NotBeNull();
        return geometry!.Bounds;
    }

    private static WriteableBitmap CreateBitmap(int width, int height, double dpi)
    {
        return new WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, null);
    }

    private static List<InkStrokeData> Adapt(
        IReadOnlyList<InkStrokeData> strokes,
        BitmapSource background,
        double fallbackScale)
    {
        var result = AdaptStrokesForBackgroundMethod.Invoke(null, new object?[] { strokes, background, fallbackScale });
        result.Should().BeOfType<List<InkStrokeData>>();
        return (List<InkStrokeData>)result!;
    }
}
