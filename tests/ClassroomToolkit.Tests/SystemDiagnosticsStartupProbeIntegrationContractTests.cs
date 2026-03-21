using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SystemDiagnosticsStartupProbeIntegrationContractTests
{
    [Fact]
    public void CollectSystemDiagnostics_ShouldIncludeStartupCompatibilityProbeSummary()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("AppendStartupCompatibilitySummary(");
        source.Should().Contain("StartupCompatibilityProbe.Collect(settingsPath, overridesJson)");
        source.Should().Contain("启动兼容探针：阻断=");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Diagnostics",
            "SystemDiagnostics.cs");
    }
}
