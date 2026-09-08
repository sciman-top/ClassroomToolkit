using System.IO;
using AwesomeAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppLogRetentionLifecycleContractTests
{
    [Fact]
    public void TryApplyErrorLogRetention_ShouldRetryWithBoundedBackoff_AndLatchOnSuccessOrGiveUp()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (Volatile.Read(ref _errorLogRetentionSucceeded) == 1)");
        source.Should().Contain("MaxErrorLogRetentionAttempts");
        source.Should().Contain("if (Interlocked.Exchange(ref _errorLogRetentionApplied, 1) == 1)");
        source.Should().Contain("Volatile.Write(ref _errorLogRetentionSucceeded, 1);");
        source.Should().Contain("Interlocked.Increment(ref _errorLogRetentionFailures);");
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
