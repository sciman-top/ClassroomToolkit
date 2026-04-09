using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class StartupCompatibilityWarningCopyContractTests
{
    [Fact]
    public void StartupWarning_ShouldContainQuickFixSteps_ForArchitectureMismatch()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("BuildStartupWarningQuickFixLines");
        source.Should().Contain("issueCodes.Contains(\"presentation-arch-mismatch\")");
        source.Should().Contain("确保同位数，建议都用 x64。");
        source.Should().Contain("若只装 x86 WPS/Office，请改装 x64 后重启。");
        source.Should().Contain("发现可降级运行风险。");
        source.Should().Contain("程序将继续启动。请尽快修复。");
        source.Should().Contain("startupCompatibilityReportPath");
        source.Should().Contain("BuildStartupSupportPayload");
        source.Should().Contain("风险码：");
        source.Should().Contain("已自动执行：");
        source.Should().Contain("建议处理：");
        source.Should().Contain("发现可降级运行风险。");
        source.Should().Contain("发现可降级运行风险：");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Startup",
            "StartupOrchestrator.cs");
    }
}
