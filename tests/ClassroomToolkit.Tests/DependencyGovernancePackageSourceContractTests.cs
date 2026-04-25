using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class DependencyGovernancePackageSourceContractTests
{
    [Theory]
    [InlineData("scripts/quality/check-dependency-upgrade-feasibility.ps1")]
    [InlineData("scripts/quality/check-dependency-vulnerabilities.ps1")]
    public void DependencyGovernanceScripts_ShouldUseExplicitTrustedPackageSource(string relativePath)
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            relativePath.Split(new[] { '/', '\\' })));

        source.Should().Contain("[string]$PackageSource = \"https://api.nuget.org/v3/index.json\"");
        source.Should().Contain("\"--source\"");
        source.Should().Contain("$PackageSource");
    }

    [Theory]
    [InlineData("scripts/quality/check-dependency-upgrade-feasibility.ps1")]
    [InlineData("scripts/quality/check-dependency-vulnerabilities.ps1")]
    public void DependencyGovernanceScripts_ShouldIncludeDotnetFailureOutput(string relativePath)
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            relativePath.Split(new[] { '/', '\\' })));

        source.Should().Contain("Format-DotnetListFailureOutput");
        source.Should().Contain("Output: $detail");
    }
}
