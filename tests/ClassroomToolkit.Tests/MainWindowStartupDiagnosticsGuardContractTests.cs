using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowStartupDiagnosticsGuardContractTests
{
    [Fact]
    public void RunStartupDiagnostics_ShouldUseEnvironmentGatePolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (!StartupDiagnosticsGatePolicy.ShouldRun(Environment.GetEnvironmentVariable(\"CTOOL_NO_STARTUP_DIAG\")))");
        source.Should().Contain("return;");
    }

    [Fact]
    public void RunStartupDiagnostics_ShouldGuardCancellationAndDispatcherShutdownBeforeDialogDispatch()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (_backgroundTasksCancellation.IsCancellationRequested || token.IsCancellationRequested)");
        source.Should().Contain("if (Dispatcher.HasShutdownStarted || Dispatcher.HasShutdownFinished)");
        source.Should().Contain("void ShowDiagnosticsDialog()");
    }

    [Fact]
    public void RunStartupDiagnostics_ShouldExecuteThroughSafeTaskRunnerWithBackgroundCancellationToken()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_ = SafeTaskRunner.Run(\"MainWindow.StartupDiagnostics\", token =>");
        source.Should().Contain("}, _backgroundTasksCancellation.Token, ex =>");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Launcher.cs");
    }
}
