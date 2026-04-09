using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkRenderSurfaceContractTests
{
    [Fact]
    public void CrossPageNeighborInkPipeline_ShouldCarryHorizontalOffsetThroughRenderAndSlotTransform()
    {
        var crossPageSource = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage*.cs");
        var rendererSource = File.ReadAllText(GetRendererSourcePath());

        crossPageSource.Should().Contain("CrossPageNeighborInkRenderSurfacePolicy.Resolve(");
        crossPageSource.Should().Contain("HorizontalOffsetDip");
        crossPageSource.Should().Contain("SetNeighborInkSlotTag(");
        rendererSource.Should().Contain("double horizontalOffsetDip = 0");
        rendererSource.Should().Contain("dc.PushTransform(new TranslateTransform(horizontalOffsetDip, 0));");
    }

    [Fact]
    public void ResolveNeighborInkBitmap_ShouldSynchronouslyRebuild_WhenInteractivePathDisablesDeferredRender()
    {
        var crossPageSource = ContractSourceAggregateLoader.LoadByPattern(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage*.cs");

        crossPageSource.Should().Contain("if (!allowDeferredRender)");
        crossPageSource.Should().Contain("var rebuiltEntry = BuildNeighborInkCacheEntry(page, pageBitmap, strokes);");
        crossPageSource.Should().Contain("return rebuiltEntry.Bitmap;");
    }

    private static string GetRendererSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Ink",
            "InkStrokeRenderer.cs");
    }
}
