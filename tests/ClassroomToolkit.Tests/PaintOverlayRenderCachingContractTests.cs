using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintOverlayRenderCachingContractTests
{
    [Fact]
    public void StoredStrokeRendering_ShouldReuseColorBrushAndOpacityMaskCaches()
    {
        var rendering = File.ReadAllText(GetSourcePath("PaintOverlayWindow.Ink.Rendering.cs"));
        var cache = File.ReadAllText(GetSourcePath("PaintOverlayWindow.Ink.Rendering.Cache.cs"));

        rendering.Should().Contain("TryGetCachedStrokeColor(stroke.ColorHex");
        rendering.Should().Contain("var brush = GetCachedSolidBrush(color);");
        rendering.Should().Contain("GetCachedInkOpacityMask(");
        cache.Should().Contain("_inkOpacityMaskCache.GetOrCreate(");
        cache.Should().Contain("InkOpacityMaskCache.PaintTextureVariant");
    }

    [Fact]
    public void PhotoBitmapLoading_ShouldProbeWidthAndDecodeFromOneStream()
    {
        var loading = File.ReadAllText(GetSourcePath("PaintOverlayWindow.Photo.Loading.cs"));

        loading.Should().Contain("using var stream = File.Open(");
        loading.Should().Contain("TryReadImagePixelWidth(stream)");
        loading.Should().Contain("bitmap.StreamSource = stream;");
        loading.Should().NotContain("TryReadImagePixelWidth(path)");
    }

    [Fact]
    public void PredictedBrushRendering_ShouldReuseFrozenPenAndBrushCaches()
    {
        var preview = File.ReadAllText(GetSourcePath("PaintOverlayWindow.Ink.Preview.cs"));

        preview.Should().Contain("private void DrawPredictedBrushSegment(");
        preview.Should().Contain("var pen0 = GetCachedPen(c0, w0);");
        preview.Should().Contain("var pen1 = GetCachedPen(c1, w1);");
        preview.Should().Contain("var tipBrush = GetCachedSolidBrush(c2);");
        preview.Should().NotContain("new MediaPen(new SolidColorBrush");
    }

    [Fact]
    public void ExportAndNeighborInkRendering_ShouldReuseColorAndSolidBrushCaches()
    {
        var renderer = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkStrokeRenderer.cs"));

        renderer.Should().Contain("ResolveStrokeColor(stroke.ColorHex)");
        renderer.Should().Contain("GetCachedSolidBrush(color)");
        renderer.Should().Contain("_strokeColorCache");
        renderer.Should().Contain("_solidBrushCache");
        renderer.Should().Contain("InkRenderingCacheDefaults.StrokeColorCacheLimit");
        renderer.Should().Contain("InkCacheRuntimeDefaults.SolidBrushCacheLimit");
    }

    [Fact]
    public void InkRedraw_ShouldBatchContiguousSimpleStrokesBeforeRenderingCalligraphy()
    {
        var rendering = File.ReadAllText(GetSourcePath("PaintOverlayWindow.Ink.Rendering.cs"));

        rendering.Should().Contain("List<DrawCommand> simpleStrokeBatch");
        rendering.Should().Contain("simpleStrokeBatch.Add(new DrawCommand(renderGeometry, brush, null, null, null));");
        rendering.Should().Contain("if (simpleStrokeBatch.Count >= 24)");
        rendering.Should().Contain("RenderAndBlendBatch(simpleStrokeBatch);");
        rendering.Should().Contain("simpleStrokeBatch.Clear();");
    }

    [Fact]
    public void CrossPageInputSwitch_ShouldForwardVisibleOrCachedTargetBitmapToInteractiveNavigation()
    {
        var inputSwitch = File.ReadAllText(GetSourcePath("PaintOverlayWindow.Input.CrossPageSwitch.cs"));

        inputSwitch.Should().Contain("TryResolveVisibleImagePageFromPointer(");
        inputSwitch.Should().Contain("visiblePage == boundedTargetPage");
        inputSwitch.Should().Contain("_neighborImageCache.TryGetValue(boundedTargetPage, out var cachedBitmap)");
        inputSwitch.Should().Contain("SwitchToImagePageForInput(currentPage, boundedTargetPage, resolvedBitmap, preloadedBitmap, input)");
    }

    private static string GetSourcePath(string fileName)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            fileName);
    }
}
