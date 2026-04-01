using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageNeighborInkRenderSurfaceContractTests
{
    [Fact]
    public void CrossPageNeighborInkPipeline_ShouldCarryHorizontalOffsetThroughRenderAndSlotTransform()
    {
        var crossPageSource = File.ReadAllText(GetCrossPageSourcePath());
        var rendererSource = File.ReadAllText(GetRendererSourcePath());

        crossPageSource.Should().Contain("CrossPageNeighborInkRenderSurfacePolicy.Resolve(");
        crossPageSource.Should().Contain("HorizontalOffsetDip");
        crossPageSource.Should().Contain("SetNeighborInkSlotTag(");
        rendererSource.Should().Contain("double horizontalOffsetDip = 0");
        rendererSource.Should().Contain("dc.PushTransform(new TranslateTransform(horizontalOffsetDip, 0));");
    }

    private static string GetCrossPageSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Paint",
            "PaintOverlayWindow.Photo.CrossPage.cs");
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
