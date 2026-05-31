using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintSettingsDialogBrushPresetContractTests
{
    [Fact]
    public void SettingsDialog_ShouldExposeThreeBrushSizeSliders_WithLivePreviewDots()
    {
        var xaml = File.ReadAllText(GetSettingsXamlPath());
        var source = ContractSourceAggregationHelper.ReadSourcesInDirectory(
            ["src", "ClassroomToolkit.App", "Paint"],
            "PaintSettingsDialog*.cs");

        xaml.Should().Contain("Text=\"笔画粗细 1\"");
        xaml.Should().Contain("x:Name=\"BrushSize2Slider\"");
        xaml.Should().Contain("x:Name=\"BrushSize3Slider\"");
        xaml.Should().Contain("x:Name=\"BrushSizePreview\"");
        xaml.Should().Contain("x:Name=\"EraserSizePreview\"");
        xaml.Should().Contain("x:Name=\"BrushOpacityPreview\"");
        xaml.Should().NotContain("Text=\"图形工具\"");
        source.Should().Contain("QuickBrushSize1 = Clamp(BrushSizeSlider.Value, 1, 50);");
        source.Should().Contain("QuickBrushSize2 = Clamp(BrushSize2Slider.Value, 1, 50);");
        source.Should().Contain("QuickBrushSize3 = Clamp(BrushSize3Slider.Value, 1, 50);");
        source.Should().Contain("UpdateCirclePreview(BrushSizePreview, BrushSizeSlider.Value");
        source.Should().Contain("BrushOpacityPreview.Opacity");
    }

    private static string GetSettingsXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Paint",
        "PaintSettingsDialog.xaml");
}
