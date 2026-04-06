using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class UiCopyContractTests
{
    [Fact]
    public void MainWindow_ShouldUseCompactPrimaryLabels()
    {
        var xaml = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        xaml.Should().Contain("Content=\"点名与计时\"");
        xaml.Should().NotContain("Content=\"点名 / 计时\"");
    }

    [Fact]
    public void SettingsDialogs_ShouldUseShortTabLabelsAndResetActions()
    {
        var paintXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));

        paintXaml.Should().Contain("Header=\"基础\"");
        paintXaml.Should().Contain("Content=\"重置本页\"");
        paintXaml.Should().Contain("Content=\"全部重置\"");
        paintXaml.Should().Contain("Content=\"转为自定义\"");
        paintXaml.Should().NotContain("Header=\"笔触与预设\"");
        paintXaml.Should().NotContain("Content=\"仅重置当前页\"");
        paintXaml.Should().NotContain("Content=\"重置全部设置\"");
        paintXaml.Should().NotContain("Content=\"切换为自定义后编辑\"");

        rollCallXaml.Should().Contain("Header=\"显示\"");
        rollCallXaml.Should().Contain("Header=\"语音\"");
        rollCallXaml.Should().Contain("Header=\"遥控\"");
        rollCallXaml.Should().Contain("Header=\"提醒\"");
        rollCallXaml.Should().Contain("Content=\"重置本页\"");
        rollCallXaml.Should().Contain("Content=\"全部重置\"");
        rollCallXaml.Should().NotContain("Header=\"显示与照片\"");
        rollCallXaml.Should().NotContain("Header=\"语音播报\"");
        rollCallXaml.Should().NotContain("Header=\"遥控按键\"");
        rollCallXaml.Should().NotContain("Header=\"倒计时提醒\"");
    }

    [Fact]
    public void ResourceWindows_ShouldUseShortHelperCopy()
    {
        var imageManagerXaml = File.ReadAllText(GetXamlPath("Photos", "ImageManagerWindow.xaml"));
        var photoOverlayXaml = File.ReadAllText(GetXamlPath("Photos", "PhotoOverlayWindow.xaml"));

        imageManagerXaml.Should().Contain("Text=\"先在左侧选择文件夹\"");
        imageManagerXaml.Should().NotContain("Text=\"当前没有可显示内容，请先在左侧选择文件夹。\"");

        photoOverlayXaml.Should().Contain("Text=\"单击空白处关闭\"");
        photoOverlayXaml.Should().NotContain("Text=\"单击背景或关闭按钮即可退出\"");
    }

    [Fact]
    public void SmallDialogs_ShouldUseShortHelperCopy()
    {
        var aboutXaml = File.ReadAllText(GetXamlPath("AboutDialog.xaml"));
        var autoExitXaml = File.ReadAllText(GetXamlPath("AutoExitDialog.xaml"));
        var classSelectXaml = File.ReadAllText(GetXamlPath("ClassSelectDialog.xaml"));

        aboutXaml.Should().Contain("Text=\"课堂常用工具\"");
        aboutXaml.Should().Contain("ToolTip=\"关闭\"");
        aboutXaml.Should().NotContain("ToolTip=\"关闭产品信息窗口\"");

        autoExitXaml.Should().Contain("Title=\"自动关闭\"");
        autoExitXaml.Should().Contain("Text=\"自动关闭\"");
        autoExitXaml.Should().Contain("Text=\"设为 0 表示不自动关闭。\"");
        autoExitXaml.Should().NotContain("Title=\"启动器设置\"");

        classSelectXaml.Should().Contain("Text=\"选择后将用于当前点名窗口。\"");
    }

    [Fact]
    public void ManagementAndPromptDialogs_ShouldUseShortTitles()
    {
        var diagnosticsXaml = File.ReadAllText(GetXamlPath("Diagnostics", "DiagnosticsDialog.xaml"));
        var startupWarningXaml = File.ReadAllText(GetXamlPath("Diagnostics", "StartupCompatibilityWarningDialog.xaml"));
        var remoteKeyXaml = File.ReadAllText(GetXamlPath("RemoteKeyDialog.xaml"));
        var inkSettingsXaml = File.ReadAllText(GetXamlPath("Ink", "InkSettingsDialog.xaml"));

        diagnosticsXaml.Should().Contain("Text=\"兼容诊断\"");
        diagnosticsXaml.Should().Contain("Content=\"恢复启动提示\"");
        diagnosticsXaml.Should().NotContain("Text=\"系统兼容性诊断\"");

        startupWarningXaml.Should().Contain("Text=\"兼容提示\"");
        startupWarningXaml.Should().Contain("Content=\"诊断报告\"");
        startupWarningXaml.Should().Contain("Content=\"复制诊断\"");

        remoteKeyXaml.Should().Contain("Title=\"遥控键\"");
        remoteKeyXaml.Should().Contain("Text=\"遥控键\"");
        remoteKeyXaml.Should().NotContain("Title=\"遥控键设置\"");

        inkSettingsXaml.Should().Contain("Title=\"笔迹记录\"");
        inkSettingsXaml.Should().Contain("Content=\"启用笔迹记录\"");
        inkSettingsXaml.Should().NotContain("Title=\"笔迹记录与回看\"");
    }

    [Fact]
    public void FloatingAndManagementWindows_ShouldUseCompactLabels()
    {
        var studentListXaml = File.ReadAllText(GetXamlPath("StudentListDialog.xaml"));
        var paletteXaml = File.ReadAllText(GetXamlPath("Paint", "QuickColorPaletteWindow.xaml"));
        var groupOverlayXaml = File.ReadAllText(GetXamlPath("Photos", "RollCallGroupOverlayWindow.xaml"));

        studentListXaml.Should().Contain("Title=\"班级名单\"");
        studentListXaml.Should().Contain("ToolTip=\"关闭\"");

        paletteXaml.Should().Contain("Title=\"快捷颜色\"");
        paletteXaml.Should().Contain("Text=\"颜色\"");
        paletteXaml.Should().NotContain("Brush_Background_L2");

        groupOverlayXaml.Should().Contain("Title=\"分组提示\"");
        groupOverlayXaml.Should().Contain("Text=\"全部\"");
    }

    private static string GetXamlPath(params string[] segments)
    {
        return TestPathHelper.ResolveAppPath(segments);
    }
}
