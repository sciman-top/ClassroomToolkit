using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowExitLifecycleContractTests
{
    [Fact]
    public void RequestExit_ShouldUseExitPlanPolicyAndLifecycleSafeOperations()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var exitPlan = MainWindowExitPlanPolicy.Resolve(");
        source.Should().Contain("if (!exitPlan.ShouldExit)");
        source.Should().Contain("ExecuteLifecycleSafe(phase, \"reset-toolbar-retouch-runtime\",");
        source.Should().Contain("ExecuteLifecycleSafe(phase, \"trigger-ink-cleanup\", TriggerInkCleanup);");
        source.Should().Contain("ExecuteLifecycleSafe(phase, \"shutdown-application\", () => System.Windows.Application.Current.Shutdown());");
    }

    [Fact]
    public void OnClosing_ShouldUseClosingPlanPolicy_AndRequestExitWhenNeeded()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var closingPlan = MainWindowOnClosingPlanPolicy.Resolve(_allowClose);");
        source.Should().Contain("e.Cancel = closingPlan.ShouldCancelClose;");
        source.Should().Contain("if (closingPlan.ShouldRequestExit)");
        source.Should().Contain("RequestExit();");
    }

    [Fact]
    public void OnClosed_ShouldGuardDuplicateDispose_AndCancelBackgroundTasksSafely()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (_backgroundTasksCancellationDisposed)");
        source.Should().Contain("_backgroundTasksCancellationDisposed = true;");
        source.Should().Contain("ExecuteLifecycleSafe(\"main-window-closed\", \"cancel-background-tasks\", () => _backgroundTasksCancellation.Cancel());");
        source.Should().Contain("_backgroundTasksCancellation.Dispose();");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
