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
        source.Should().Contain("确保程序与 Office/WPS 同位数（建议全部使用 x64）。");
        source.Should().Contain("若当前仅安装了 x86 WPS/Office，请改装 x64 版本后再重启。");
        source.Should().Contain("startupCompatibilityReportPath");
        source.Should().Contain("BuildStartupSupportPayload");
        source.Should().Contain("风险码：");
        source.Should().Contain("已自动执行：");
        source.Should().Contain("建议处理：");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }
}
