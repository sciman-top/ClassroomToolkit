using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WindowsProcessEnvironmentBootstrapContractTests
{
    [Fact]
    public void BootstrapScript_ShouldFillMissingWindowsProcessEnvironmentValues()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "env",
            "Initialize-WindowsProcessEnvironment.ps1"));

        source.Should().Contain("[Environment]::SetEnvironmentVariable($Name, $Value, \"Process\")");
        source.Should().Contain("\"SystemRoot\"");
        source.Should().Contain("\"windir\"");
        source.Should().Contain("\"ComSpec\"");
        source.Should().Contain("\"APPDATA\"");
        source.Should().Contain("\"LOCALAPPDATA\"");
        source.Should().Contain("\"ProgramData\"");
        source.Should().Contain("\"ProgramFiles\"");
        source.Should().Contain("\"ProgramFiles(x86)\"");
        source.Should().Contain("\"CommonProgramFiles\"");
        source.Should().Contain("\"CommonProgramFiles(x86)\"");
        source.Should().Contain("\"NUGET_PACKAGES\"");
    }

    [Theory]
    [InlineData("scripts/quality/run-local-quality-gates.ps1")]
    [InlineData("scripts/release/preflight-check.ps1")]
    [InlineData("scripts/validation/run-compatibility-preflight.ps1")]
    [InlineData("scripts/validation/run-stable-tests.ps1")]
    [InlineData("scripts/validation/validate-stable-test-config.ps1")]
    [InlineData("scripts/quality/check-dependency-vulnerabilities.ps1")]
    [InlineData("scripts/quality/check-dependency-upgrade-feasibility.ps1")]
    [InlineData("scripts/quality/check-analyzer-backlog-baseline.ps1")]
    [InlineData("scripts/validation/run-compatibility-matrix-report.ps1")]
    [InlineData("scripts/validation/collect-settings-load-performance-samples.ps1")]
    [InlineData("scripts/release/prepare-distribution.ps1")]
    [InlineData("scripts/validation/run-final-acceptance-evidence.ps1")]
    [InlineData("scripts/collect-brush-quality-baseline.ps1")]
    [InlineData("scripts/collect-brush-telemetry-report.ps1")]
    [InlineData("scripts/ctoolkit.ps1")]
    [InlineData("scripts/automation/run-safe-autopilot.ps1")]
    public void DotnetEntrypoints_ShouldLoadWindowsProcessEnvironmentBootstrap(string relativePath)
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            relativePath.Split(new[] { '/', '\\' })));

        source.Should().Contain("Initialize-WindowsProcessEnvironment.ps1");
        source.Should().Contain(". $environmentBootstrap");
    }
}
