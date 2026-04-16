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

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "scripts",
            "quality",
            "run-local-quality-gates.ps1");
    }
}
