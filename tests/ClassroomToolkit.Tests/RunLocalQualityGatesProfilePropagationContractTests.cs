using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RunLocalQualityGatesProfilePropagationContractTests
{
    [Fact]
    public void StableTestsStep_ShouldPassThrough_SelectedProfile()
    {
        var source = File.ReadAllText(GetSourcePath());

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
