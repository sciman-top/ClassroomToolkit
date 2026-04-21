using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ImageManagerTouchFlowContractTests
{
    [Fact]
    public void ImageManager_ShouldExposeVisibleSelectionMode_AndKeepSingleTapOpen()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var source = File.ReadAllText(GetSourcePath());

        xaml.Should().Contain("x:Name=\"EnterSelectionModeButton\"");
        xaml.Should().Contain("Click=\"OnEnterSelectionModeClick\"");
        xaml.Should().Contain("x:Name=\"DeleteFilesButton\"");
        xaml.Should().Contain("ToolTip=\"取消收藏（保留最近）\"");
        xaml.Should().Contain("ToolTip=\"加入收藏\"");
        xaml.Should().Contain("Style=\"{StaticResource Style_IconButton}\"");
        source.Should().Contain("private void OnEnterSelectionModeClick");
        source.Should().Contain("EnterMultiSelectMode(");
        source.Should().Contain("ImageManagerActivationPolicy.ShouldOpenOnSingleClick");
        source.Should().Contain("if (item.IsFolder)");
        source.Should().Contain("OpenFolder(item.Path);");
    }

    [Fact]
    public void ImageManager_ShouldUseUnifiedToolbarTouchStyles()
    {
        var xaml = File.ReadAllText(GetXamlPath());

        xaml.Should().Contain("Style_ImageManagerToolbarButton");
        xaml.Should().Contain("Style_ImageManagerToolbarDangerButton");
        xaml.Should().Contain("Style_ImageManagerToolbarToggleButton");
        xaml.Should().Contain("Style_ImageManagerToolbarIconButton");
        xaml.Should().Contain("Gradient_Success");
        xaml.Should().NotContain("Gradient_Teal");
        xaml.Should().Contain("x:Name=\"BackButton\"");
        xaml.Should().Contain("Style=\"{StaticResource Style_ImageManagerToolbarIconButton}\"");
        xaml.Should().Contain("x:Name=\"ListViewButton\"");
        xaml.Should().Contain("Style=\"{StaticResource Style_ImageManagerToolbarToggleButton}\"");
        xaml.Should().Contain("x:Name=\"EnterSelectionModeButton\"");
        xaml.Should().Contain("Style=\"{StaticResource Style_ImageManagerToolbarButton}\"");
        xaml.Should().Contain("<UniformGrid Grid.Row=\"0\" Margin=\"0,0,0,6\" Columns=\"3\">");
        xaml.Should().NotContain("Content=\"添加收藏\"\r\n                                    MinWidth=\"86\"");
        xaml.Should().NotContain("Content=\"取消收藏\"\r\n                                    MinWidth=\"86\"");
        xaml.Should().NotContain("Content=\"清空最近\"\r\n                                    MinWidth=\"86\"");
    }

    [Fact]
    public void ImageManager_ThumbnailGrid_ShouldUseVirtualizingWrapPanel()
    {
        var xaml = File.ReadAllText(GetXamlPath());
        var panelSource = File.ReadAllText(GetVirtualizingWrapPanelPath());

        xaml.Should().Contain("<local:VirtualizingWrapPanel/>");
        xaml.Should().Contain("ScrollViewer.CanContentScroll=\"True\"");
        xaml.Should().Contain("VirtualizingPanel.IsVirtualizing=\"True\"");
        xaml.Should().NotContain("<WrapPanel IsItemsHost=\"True\"");
        panelSource.Should().Contain("public sealed class VirtualizingWrapPanel");
        panelSource.Should().Contain("VirtualizingPanel, IScrollInfo");
        panelSource.Should().Contain("CoerceNonNegativeDimension");
        panelSource.Should().Contain("double.IsNaN(value) || value < 0");
    }

    private static string GetXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "ImageManagerWindow.xaml");

    private static string GetSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "ImageManagerWindow.Navigation.cs");

    private static string GetVirtualizingWrapPanelPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "Photos",
        "VirtualizingWrapPanel.cs");
}
