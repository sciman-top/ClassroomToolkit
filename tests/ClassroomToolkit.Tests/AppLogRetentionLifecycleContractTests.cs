using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppLogRetentionLifecycleContractTests
{
    [Fact]
    public void TryApplyErrorLogRetention_ShouldRetryAfterRecoverableFailure_AndOnlyLatchOnSuccess()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Volatile.Read(ref _errorLogRetentionSucceeded) == 1)");
        source.Should().Contain("if (Interlocked.Exchange(ref _errorLogRetentionApplied, 1) == 1)");
        source.Should().Contain("Volatile.Write(ref _errorLogRetentionSucceeded, 1);");
        source.Should().Contain("Volatile.Write(ref _errorLogRetentionSucceeded, 0);");
        source.Should().Contain("Interlocked.Exchange(ref _errorLogRetentionApplied, 0);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }
}
