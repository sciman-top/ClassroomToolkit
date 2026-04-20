using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherOverflowContractTests
{
    [Fact]
    public void MainWindow_ShouldUseSharedOverflow_ForLowFrequencySettings()
    {
        var xaml = File.ReadAllText(GetMainWindowXamlPath());
        var source = File.ReadAllText(GetLauncherSourcePath());

        xaml.Should().Contain("x:Name=\"LauncherMoreMenu\"");
        xaml.Should().Contain("Header=\"自动关闭\"");
        xaml.Should().Contain("Header=\"画笔设置\"");
        xaml.Should().Contain("Header=\"点名设置\"");
        xaml.Should().NotContain("ToolTip=\"画笔（长按设置）\"");
        xaml.Should().NotContain("ToolTip=\"点名与计时（长按设置）\"");
        source.Should().Contain("OpenLauncherMoreMenu()");
        source.Should().Contain("OnLauncherPaintSettingsMenuClick");
        source.Should().Contain("OnLauncherRollCallSettingsMenuClick");
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
