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
        paintXaml.Should().Contain("Content=\"切换为自定义\"");
        paintXaml.Should().Contain("Content=\"导出包\"");
        paintXaml.Should().Contain("Content=\"导入包\"");
        paintXaml.Should().Contain("Content=\"复制包\"");
        paintXaml.Should().Contain("Content=\"粘贴导入\"");
        paintXaml.Should().Contain("Content=\"撤销导入\"");
        paintXaml.Should().Contain("Content=\"滚轮翻页\"");
        paintXaml.Should().Contain("Content=\"全屏置顶\"");
        paintXaml.Should().NotContain("Header=\"笔触与预设\"");
        paintXaml.Should().NotContain("Content=\"仅重置当前页\"");
        paintXaml.Should().NotContain("Content=\"重置全部设置\"");
        paintXaml.Should().NotContain("Content=\"切换为自定义后编辑\"");
        paintXaml.Should().NotContain("Content=\"导出规则包\"");
        paintXaml.Should().NotContain("Content=\"导入规则包\"");
        paintXaml.Should().NotContain("Content=\"复制规则包\"");
        paintXaml.Should().NotContain("Content=\"粘贴并导入\"");
        paintXaml.Should().NotContain("Content=\"撤销最近导入\"");
        paintXaml.Should().NotContain("Content=\"滚轮映射为翻页键\"");
        paintXaml.Should().NotContain("Content=\"全屏放映自动置顶\"");

        rollCallXaml.Should().Contain("Header=\"显示\"");
        rollCallXaml.Should().Contain("Header=\"语音\"");
        rollCallXaml.Should().Contain("Header=\"遥控\"");
        rollCallXaml.Should().Contain("Header=\"提醒\"");
        rollCallXaml.Should().Contain("Text=\"调整姓名、学号、照片。\"");
        rollCallXaml.Should().Contain("Text=\"控制朗读、发音人、提醒声。\"");
        rollCallXaml.Should().Contain("Text=\"按需设置。\"");
        rollCallXaml.Should().Contain("Content=\"显示学生照片\"");
        rollCallXaml.Should().Contain("Content=\"点名读姓名\"");
        rollCallXaml.Should().Contain("Text=\"播报设备默认跟随系统。\"");
        rollCallXaml.Should().Contain("Text=\"设置翻页笔和遥控。\"");
        rollCallXaml.Should().Contain("Content=\"用翻页笔切组\"");
        rollCallXaml.Should().Contain("Content=\"结束播放音效\"");
        rollCallXaml.Should().Contain("Content=\"中途提醒\"");
        rollCallXaml.Should().Contain("Text=\"设置结束和中途提醒。\"");
        rollCallXaml.Should().Contain("Text=\"开启后按间隔提醒。\"");
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

        imageManagerXaml.Should().Contain("Text=\"先选左侧文件夹\"");
        imageManagerXaml.Should().Contain("ToolTip=\"输入后回车\"");
        imageManagerXaml.Should().Contain("ToolTip=\"列表\"");
        imageManagerXaml.Should().Contain("ToolTip=\"缩略图\"");
        imageManagerXaml.Should().NotContain("Text=\"当前没有可显示内容，请先在左侧选择文件夹。\"");

        photoOverlayXaml.Should().Contain("Text=\"点击空白关闭\"");
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
        autoExitXaml.Should().Contain("Text=\"0 表示不自动关闭。\"");
        autoExitXaml.Should().NotContain("Title=\"启动器设置\"");

        classSelectXaml.Should().Contain("Text=\"选择后应用到当前点名窗口。\"");
    }

    [Fact]
    public void ManagementAndPromptDialogs_ShouldUseShortTitles()
    {
        var diagnosticsXaml = File.ReadAllText(GetXamlPath("Diagnostics", "DiagnosticsDialog.xaml"));
        var startupWarningXaml = File.ReadAllText(GetXamlPath("Diagnostics", "StartupCompatibilityWarningDialog.xaml"));
        var remoteKeyXaml = File.ReadAllText(GetXamlPath("RemoteKeyDialog.xaml"));
        var inkSettingsXaml = File.ReadAllText(GetXamlPath("Ink", "InkSettingsDialog.xaml"));

        diagnosticsXaml.Should().Contain("Text=\"兼容诊断\"");
        diagnosticsXaml.Should().Contain("Content=\"恢复提示\"");
        diagnosticsXaml.Should().Contain("Content=\"导出包\"");
        diagnosticsXaml.Should().Contain("Content=\"复制结果\"");
        diagnosticsXaml.Should().NotContain("Text=\"系统兼容性诊断\"");
        diagnosticsXaml.Should().NotContain("Content=\"导出诊断包\"");

        startupWarningXaml.Should().Contain("Text=\"兼容提示\"");
        startupWarningXaml.Should().Contain("Content=\"本问题不再提示\"");
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
