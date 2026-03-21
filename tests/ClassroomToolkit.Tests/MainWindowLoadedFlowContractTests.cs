using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowLoadedFlowContractTests
{
    [Fact]
    public void OnLoaded_ShouldResolveToggleAction_FromLoadedTogglePolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var toggleAction = MainWindowLoadedToggleActionPolicy.Resolve(_settings.LauncherMinimized);");
    }

    [Fact]
    public void OnLoaded_ShouldBranchToMinimizeLauncherOrUpdateToggleButtons()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (toggleAction == MainWindowLoadedToggleAction.MinimizeLauncher)");
        source.Should().Contain("MinimizeLauncher(fromSettings: true);");
        source.Should().Contain("UpdateToggleButtons();");
    }

    [Fact]
    public void OnLoaded_ShouldRunCoreStartupSequence()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("ApplyLauncherPosition();");
        source.Should().Contain("WindowPlacementHelper.EnsureVisible(this);");
        source.Should().Contain("ScheduleAutoExitTimer();");
        source.Should().Contain("ScheduleInkCleanup();");
        source.Should().Contain("WarmupRollCallData();");
        source.Should().Contain("RunStartupDiagnostics();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
