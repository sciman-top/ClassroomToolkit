using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowInteropRetryExecutorContractTests
{
    [Fact]
    public void WaitBeforeRetry_ShouldUseCancellationWaitHandle_InsteadOfThreadSleep()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("cancellationToken.WaitHandle.WaitOne(retrySleepMs)");
        source.Should().NotContain("Thread.Sleep(");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Windowing",
            "WindowInteropRetryExecutor.cs");
    }
}

