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
        var widgetStyles = File.ReadAllText(GetWidgetStylesPath());

        xaml.Should().Contain("PreviewMouseLeftButtonDown=\"OnQuickColorPointerDown\"");
        xaml.Should().Contain("PreviewTouchDown=\"OnQuickColorTouchDown\"");
        xaml.Should().Contain("PreviewMouseLeftButtonDown=\"OnShapePointerDown\"");
        xaml.Should().Contain("PreviewTouchDown=\"OnShapeTouchDown\"");
        xaml.Should().Contain("PreviewTouchDown=\"OnToolbarTouchDragStart\"");
        xaml.Should().NotContain("<Setter Property=\"MinWidth\" Value=\"30\"/>");
        xaml.Should().NotContain("<Setter Property=\"MinHeight\" Value=\"30\"/>");
        xaml.Should().Contain("ToolTip=\"画笔 1：点按使用，再点/长按换色和粗细\"");
        xaml.Should().Contain("ToolTip=\"图形：点按使用，长按选择\"");
        xaml.Should().Contain("ToolTipService.Placement\" Value=\"Top\"");
        source.Should().Contain("ToolbarSecondTapIntentPolicy.Resolve(");
        source.Should().Contain("ApplyToolbarTouchMetrics();");
        source.Should().Contain("Math.Ceiling(44.0 / scale)");
        source.Should().Contain("OpenQuickColorDialog(index.Value);");
        source.Should().Contain("ApplyQuickBrushSizeSelection(index, selectedSizeIndex);");
        source.Should().NotContain("ApplyQuickColorSelection(selectedSizeIndex);");
        source.Should().Contain("SetQuickBrushSizeSlot(quickColorIndex, selectedBrushSize);");
        source.Should().Contain("_brushSize = _quickBrushSizes[index];");
        source.Should().NotContain("ResolveToolbarBrushPreviewSize");
        source.Should().NotContain("button.FontSize =");
        source.Should().Contain("OpenShapeMenu();");
        source.Should().Contain("GetQuickColorDisplayName");
        source.Should().Contain("GetShapeDisplayName");
        paletteSource.Should().Contain("Width = 36");
        paletteSource.Should().Contain("Height = 36");
        paletteSource.Should().Contain("ToolTip = $\"选择{option.Name}\"");
        paletteSource.Should().Contain("SelectedBrushSizeIndex");
        paletteSource.Should().Contain("BuildBrushSizeButtons");
        paletteSource.Should().Contain("BorderThickness = new Thickness(isSelected ? 3 : 1)");
        paletteSource.Should().Contain("BuildBrushSizePreview(option.Size, isSelected)");
        widgetStyles.Should().Contain("<Grid Background=\"Transparent\" MinWidth=\"{TemplateBinding MinWidth}\" MinHeight=\"{TemplateBinding MinHeight}\">");
        widgetStyles.Should().Contain("Width=\"22\"");
        widgetStyles.Should().Contain("Height=\"22\"");
        widgetStyles.Should().Contain("BorderBrush=\"{TemplateBinding Foreground}\"");
        widgetStyles.Should().Contain("Stroke=\"{StaticResource Brush_Border_Focus}\"");
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

    private static string GetWidgetStylesPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Assets",
        "Styles",
        "WidgetStyles.xaml");
}
