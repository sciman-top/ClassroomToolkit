using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;
using System.Text.Json;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityReportTests
{
    [Fact]
    public void BuildMessage_ShouldReturnPassText_WhenNoIssues()
    {
        var report = new StartupCompatibilityReport(Array.Empty<StartupCompatibilityIssue>());

        var message = report.BuildMessage(includeWarnings: true);

        message.Should().Be("环境检查通过。");
        report.HasBlockingIssues.Should().BeFalse();
        report.HasWarnings.Should().BeFalse();
    }

    [Fact]
    public void BuildMessage_ShouldHideWarnings_WhenIncludeWarningsIsFalse()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    Code: "warning-only",
                    Message: "仅提示",
                    Suggestion: "可忽略",
                    IsBlocking: false),
                new StartupCompatibilityIssue(
                    Code: "blocking",
                    Message: "阻断问题",
                    Suggestion: "必须修复",
                    IsBlocking: true)
            });

        var message = report.BuildMessage(includeWarnings: false);

        message.Should().Contain("[阻断] 阻断问题");
        message.Should().Contain("建议：必须修复");
        message.Should().NotContain("仅提示");
        report.HasBlockingIssues.Should().BeTrue();
        report.HasWarnings.Should().BeTrue();
    }

    [Fact]
    public void ToJson_ShouldContainIssueCodes_AndEnvironmentNode()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    Code: "presentation-privilege-mismatch",
                    Message: "权限不一致",
                    Suggestion: "统一权限启动",
                    IsBlocking: true)
            });

        var json = report.ToJson(indented: false);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        root.GetProperty("environment").GetProperty("dotnetRuntime").GetString()
            .Should().NotBeNullOrWhiteSpace();
        root.GetProperty("hasBlockingIssues").GetBoolean().Should().BeTrue();
        root.GetProperty("issues")[0].GetProperty("code").GetString()
            .Should().Be("presentation-privilege-mismatch");
    }
}
