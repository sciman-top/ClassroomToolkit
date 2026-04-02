using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class UiCopyContractTests
{
    [Fact]
    public void MainWindow_ShouldUseConciseLauncherTooltips()
    {
        var xaml = File.ReadAllText(GetXamlPath("MainWindow.xaml"));

        xaml.Should().Contain("开启画笔（长按可调设置）");
        xaml.Should().Contain("打开点名/倒计时（长按可调设置）");
        xaml.Should().Contain("打开系统兼容性诊断");
        xaml.Should().Contain("打开全局设置");
        xaml.Should().Contain("查看产品信息");
        xaml.Should().Contain("Text=\"sciman课堂工具箱\"");

        xaml.IndexOf("x:Name=\"DiagnosticsButton\"", StringComparison.Ordinal)
            .Should()
            .BeLessThan(xaml.IndexOf("x:Name=\"AboutButton\"", StringComparison.Ordinal));
        xaml.IndexOf("x:Name=\"AboutButton\"", StringComparison.Ordinal)
            .Should()
            .BeLessThan(xaml.IndexOf("x:Name=\"SettingsButton\"", StringComparison.Ordinal));
    }

    [Fact]
    public void RollCallWindow_ShouldUseClearActionCopy()
    {
        var xaml = File.ReadAllText(GetXamlPath("RollCallWindow.xaml"));

        xaml.Should().Contain("切换点名/倒计时");
        xaml.Should().Contain("打开点名设置");
        xaml.Should().Contain("隐藏点名（功能继续）");
        xaml.Should().Contain("关闭点名窗口");
        xaml.Should().Contain("正在加载名单...");
    }

    [Fact]
    public void ManagementAndOverlayWindows_ShouldUseClearHelperCopy()
    {
        var imageManagerXaml = File.ReadAllText(GetXamlPath("Photos", "ImageManagerWindow.xaml"));
        var photoOverlayXaml = File.ReadAllText(GetXamlPath("Photos", "PhotoOverlayWindow.xaml"));
        var paintOverlayXaml = File.ReadAllText(GetXamlPath("Paint", "PaintOverlayWindow.xaml"));
        var diagnosticsXaml = File.ReadAllText(GetXamlPath("Diagnostics", "DiagnosticsDialog.xaml"));

        imageManagerXaml.Should().Contain("当前没有可显示内容，请先在左侧选择文件夹。");
        imageManagerXaml.Should().Contain("资源管理视图");
        photoOverlayXaml.Should().Contain("单击背景或关闭按钮即可退出");
        paintOverlayXaml.Should().Contain("PDF/图片预览");
        paintOverlayXaml.Should().Contain("按宽度适配并居中");
        paintOverlayXaml.Should().Contain("导出当前目录的笔迹图片");
        paintOverlayXaml.Should().Contain("支持翻页、适宽、导出");
        diagnosticsXaml.Should().Contain("诊断详情仅用于本机兼容性排查。");
    }

    [Fact]
    public void SettingsDialogs_ShouldUseConciseLabelsAndTooltips()
    {
        var paintSettingsXaml = File.ReadAllText(GetXamlPath("Paint", "PaintSettingsDialog.xaml"));
        var rollCallSettingsXaml = File.ReadAllText(GetXamlPath("RollCallSettingsDialog.xaml"));
        var remoteKeyXaml = File.ReadAllText(GetXamlPath("RemoteKeyDialog.xaml"));
        var autoExitXaml = File.ReadAllText(GetXamlPath("AutoExitDialog.xaml"));
        var aboutXaml = File.ReadAllText(GetXamlPath("AboutDialog.xaml"));
        var timerSetXaml = File.ReadAllText(GetXamlPath("TimerSetDialog.xaml"));

        paintSettingsXaml.Should().Contain("PDF/图片");
        rollCallSettingsXaml.Should().Contain("关闭点名设置");
        rollCallSettingsXaml.Should().Contain("结束音效");
        remoteKeyXaml.Should().Contain("预设键位");
        remoteKeyXaml.Should().Contain("关闭遥控键设置");
        autoExitXaml.Should().Contain("设为 0 时不自动关闭。");
        aboutXaml.Should().Contain("复制软件信息");
        aboutXaml.Should().Contain("交流群");
        aboutXaml.Should().Contain("版本 -");
        paintSettingsXaml.Should().Contain("工具");
        paintSettingsXaml.Should().Contain("WPS 演示文稿");
        timerSetXaml.Should().Contain("倒计时设置");
        timerSetXaml.Should().Contain("常用时长");
        timerSetXaml.Should().Contain("关闭倒计时设置");
        timerSetXaml.Should().Contain("可输入 0-150，滑杆范围为 0-25。");
        aboutXaml.Should().Contain("Title=\"产品信息\"");
        aboutXaml.Should().Contain("Text=\"产品信息\"");
        aboutXaml.Should().Contain("关闭产品信息窗口");
        aboutXaml.Should().NotContain("关于窗口");
    }

    private static string GetXamlPath(params string[] segments)
    {
        return TestPathHelper.ResolveAppPath(segments);
    }
}
