using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RunLocalQualityGatesProfilePropagationContractTests
{
    [Fact]
    public void StableTestsStep_ShouldPassThrough_SelectedProfile()
    {
        var sourcePath = GetSourcePath();
        if (!File.Exists(sourcePath))
        {
            // Governance scripts can be intentionally uninstalled from this repo.
            // In that mode, this propagation contract is not applicable.
            return;
        }

        var source = File.ReadAllText(sourcePath);

        source.Should().Contain("-Profile $Profile");
        source.Should().NotContain("-Profile quick");
    }

    [Fact]
    public void RetryDetection_ShouldRemainCompatible_WithWindowsPowerShell()
    {
        var sourcePath = GetSourcePath();
        if (!File.Exists(sourcePath))
        {
            // Governance scripts can be intentionally uninstalled from this repo.
            // In that mode, this retry contract is not applicable.
            return;
        }

        var source = File.ReadAllText(sourcePath);

        source.Should().Contain("function Test-ContainsOrdinalIgnoreCase");
        source.Should().Contain(".IndexOf($Value, [StringComparison]::OrdinalIgnoreCase) -ge 0");
        source.Should().NotContain(".Contains(\"because it is being used by another process\", [StringComparison]::OrdinalIgnoreCase)");
        source.Should().NotContain(".Contains(\"\u5df2\u88ab\u53e6\u4e00\u8fdb\u7a0b\u4f7f\u7528\", [StringComparison]::OrdinalIgnoreCase)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "scripts",
            "quality",
            "run-local-quality-gates.ps1");
    }
}
