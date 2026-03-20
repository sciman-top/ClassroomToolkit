using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropBackgroundDispatchContractTests
{
    [Fact]
    public void InteropBackgroundDispatchExecutor_ShouldUseSafeOnErrorDispatch_ForWorkerFailurePath()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("InvokeOnErrorSafely(executorSource, executorOnError, ex, \"worker-error-callback\")");
    }

    [Fact]
    public void InteropBackgroundDispatchExecutor_ShouldUseSafeOnErrorDispatch_ForQueueFailurePath()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("InvokeOnErrorSafely(normalizedSource, onError, ex, \"queue-error-callback\")");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.Interop",
            "Utilities",
            "InteropBackgroundDispatchExecutor.cs");
    }
}
