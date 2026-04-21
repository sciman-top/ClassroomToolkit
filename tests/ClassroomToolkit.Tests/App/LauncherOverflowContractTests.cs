using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherOverflowContractTests
{
    [Fact]
    public void MainWindow_SettingsButton_ShouldOpenAutoExitDirectly()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());
        var source = File.ReadAllText(GetLauncherSourcePath());

        xaml.Should().Contain("x:Name=\"SettingsButton\"");
        xaml.Should().Contain("Click=\"OnLauncherMoreClick\"");
        xaml.Should().NotContain("x:Name=\"LauncherMoreMenu\"");
        xaml.Should().NotContain("Header=\"自动关闭\"");
        xaml.Should().NotContain("Header=\"画笔设置\"");
        xaml.Should().NotContain("Header=\"点名设置\"");
        xaml.Should().NotContain("ToolTip=\"画笔（长按设置）\"");
        xaml.Should().NotContain("ToolTip=\"点名与计时（长按设置）\"");
        source.Should().Contain("OpenAutoExitDialog();");
        source.Should().NotContain("OpenLauncherMoreMenu()");
        source.Should().NotContain("OnLauncherAutoExitMenuClick");
        source.Should().NotContain("OnLauncherPaintSettingsMenuClick");
        source.Should().NotContain("OnLauncherRollCallSettingsMenuClick");
    }

    private static string GetMainWindowXamlPath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "MainWindow.xaml");

    private static string GetLauncherSourcePath() => TestPathHelper.ResolveRepoPath(
        "src",
        "ClassroomToolkit.App",
        "MainWindow.Launcher.cs");
}
