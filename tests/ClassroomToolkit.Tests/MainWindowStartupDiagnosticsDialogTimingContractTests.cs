using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowStartupDiagnosticsDialogTimingContractTests
{
    [Fact]
    public void RunStartupDiagnostics_ShouldDelayAndGateDialog_WhenWindowNotReadyForUserAttention()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("await Task.Delay(");
        source.Should().Contain("MainWindowRuntimeDefaults.StartupDiagnosticsDialogDelayMs");
        source.Should().Contain("if (!IsLoaded || !IsVisible || WindowState == WindowState.Minimized)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Launcher.cs");
    }
}
