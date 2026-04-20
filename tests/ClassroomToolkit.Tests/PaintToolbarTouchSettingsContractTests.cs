using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PaintToolbarTouchSettingsContractTests
{
    [Fact]
    public void Toolbar_ShouldWireRepeatTapPreviewHandlers_AndKeepCompactPopoverAccess()
    {
        var xaml = File.ReadAllText(GetToolbarXamlPath());
        var source = GetToolbarSource();
        var paletteSource = File.ReadAllText(GetPaletteSourcePath());

        xaml.Should().Contain("PreviewMouseLeftButtonDown=\"OnQuickColorPointerDown\"");
        xaml.Should().Contain("PreviewTouchDown=\"OnQuickColorTouchDown\"");
        xaml.Should().Contain("PreviewMouseLeftButtonDown=\"OnShapePointerDown\"");
        xaml.Should().Contain("PreviewTouchDown=\"OnShapeTouchDown\"");
        xaml.Should().Contain("ToolTip=\"颜色 1：黑色。点按使用，再点/长按换色\"");
        xaml.Should().Contain("ToolTip=\"图形：当前直线。点按使用，再点/长按选择\"");
        xaml.Should().Contain("ToolTipService.Placement\" Value=\"Top\"");
        source.Should().Contain("ToolbarSecondTapIntentPolicy.Resolve(");
        source.Should().Contain("OpenQuickColorDialog(index.Value);");
        source.Should().Contain("OpenShapeMenu();");
        source.Should().Contain("GetQuickColorDisplayName");
        source.Should().Contain("GetShapeDisplayName");
        paletteSource.Should().Contain("Width = 36");
        paletteSource.Should().Contain("Height = 36");
        paletteSource.Should().Contain("ToolTip = $\"选择{option.Name}\"");
    }

    private static string GetToolbarXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Paint",
        "PaintToolbarWindow.xaml");

    private static string GetToolbarSource() => ContractSourceAggregateLoader.LoadByPattern(
        "src",
        "ClassroomToolkit.App",
        "Paint",
        "PaintToolbarWindow*.cs");

    private static string GetPaletteSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Paint",
        "QuickColorPaletteWindow.xaml.cs");
}
