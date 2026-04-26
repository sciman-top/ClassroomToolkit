using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class PilotMetricsScriptContractTests
{
    [Fact]
    public void CollectPilotMetrics_ShouldUseValidDotNetClrMemoryCounterPath()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "validation",
            "collect-pilot-metrics.ps1"));

        source.Should().Contain("\"\\.NET CLR Memory($($dotnetProc.ProcessName))\\% Time in GC\"");
        source.Should().NotContain("\"\\ .NET CLR Memory");
    }

    [Fact]
    public void CollectPilotMetrics_ShouldTreatLogPathsAsLiteralPaths()
    {
        var source = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "scripts",
            "validation",
            "collect-pilot-metrics.ps1"));

        source.Should().Contain("Get-ChildItem -LiteralPath $LogRoot");
        source.Should().Contain("Get-Content -LiteralPath $f.FullName");
        source.Should().Contain("Set-Content -LiteralPath $report");
        source.Should().NotContain("Get-ChildItem -Path $LogRoot");
        source.Should().NotContain("Get-Content -Path $f.FullName");
        source.Should().NotContain("Set-Content -Path $report");
    }
}
