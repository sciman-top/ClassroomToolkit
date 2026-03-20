using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class FileLoggerProviderShutdownSafetyContractTests
{
    [Fact]
    public void Dispose_ShouldUseBoundedWaitHelper_ForQueueWorkerShutdown()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("QueueDrainTimeoutMs = 3000");
        source.Should().Contain("QueueCancelGraceTimeoutMs = 1000");
        source.Should().Contain("WaitTaskSafely(_processQueueTask, QueueDrainTimeoutMs)");
        source.Should().Contain("WaitTaskSafely(_processQueueTask, QueueCancelGraceTimeoutMs)");
        source.Should().Contain("private static bool WaitTaskSafely(Task task, int timeoutMs)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Infra",
            "Logging",
            "FileLoggerProvider.cs");
    }
}
