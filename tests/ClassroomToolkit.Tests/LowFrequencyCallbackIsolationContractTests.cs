using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class LowFrequencyCallbackIsolationContractTests
{
    [Fact]
    public void SafeDragMoveFailureCallback_ShouldBeIsolated()
    {
        var source = File.ReadAllText(GetAppSourcePath("Helpers", "WindowExtensions.cs"));

        source.Should().Contain("WindowExtensions.SafeDragMove failure callback failed");
        source.Should().Contain("catch (Exception callbackEx) when (AppGlobalExceptionHandlingPolicy.IsNonFatal(callbackEx))");
    }

    [Fact]
    public void SettingsMigrationLogCallback_ShouldBeIsolated()
    {
        var source = File.ReadAllText(GetAppSourcePath("Settings", "SettingsDocumentBootstrapMigrationExecutor.cs"));

        source.Should().Contain("private static void TryLog(Action<string>? log, string message)");
        source.Should().Contain("SettingsDocumentBootstrapMigrationExecutor log callback failed");
    }

    [Fact]
    public void FloatingDispatchQueueCallbacks_ShouldUseSafeActionExecutor()
    {
        var source = File.ReadAllText(GetAppSourcePath("Windowing", "FloatingDispatchQueueExecutor.cs"));

        source.Should().Contain("SafeActionExecutionExecutor.TryExecute(");
        source.Should().Contain("() => onDispatchFailure?.Invoke(dispatchFailure)");
        source.Should().Contain("() => onDecision?.Invoke(failedDecision)");
        source.Should().Contain("() => onDecision?.Invoke(decision)");
    }

    private static string GetAppSourcePath(string folder, string file)
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            folder,
            file);
    }
}
