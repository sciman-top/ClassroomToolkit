using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SystemDiagnosticsClassifierOverrideWarningContractTests
{
    [Fact]
    public void CollectSystemDiagnostics_ShouldProminentlyReportInvalidClassifierOverrides()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("演示识别规则覆盖配置格式无效");
        source.Should().Contain("issues.Add(");
        source.Should().Contain("fixes.Add(");
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
