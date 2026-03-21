using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowStartupCleanupFlowContractTests
{
    [Fact]
    public void OnLoaded_ShouldTriggerAutoExitInkCleanupAndStartupDiagnostics()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("ScheduleAutoExitTimer();");
        source.Should().Contain("ScheduleInkCleanup();");
        source.Should().Contain("RunStartupDiagnostics();");
    }

    [Fact]
    public void ScheduleInkCleanup_ShouldRunThroughSafeTaskRunner_WithCancellationToken()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_ = SafeTaskRunner.Run(");
        source.Should().Contain("\"MainWindow.ScheduleInkCleanup\",");
        source.Should().Contain("_ => TriggerInkCleanup(),");
        source.Should().Contain("_backgroundTasksCancellation.Token,");
    }

    [Fact]
    public void TriggerInkCleanup_ShouldResolveCandidatesAndUseCleanupServices()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var candidates = InkCleanupCandidateDirectoryPolicy.Resolve(");
        source.Should().Contain("_inkPersistenceService.CleanupOrphanSidecarsInDirectory(directory);");
        source.Should().Contain("_inkExportService.CleanupOrphanCompositeOutputsInDirectory(directory);");
        source.Should().Contain("InkStartupCleanupLogPolicy.ShouldLogDeletionSummary(summary)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
