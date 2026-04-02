using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityStatusPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNormal_WhenNoIssues()
    {
        var report = new StartupCompatibilityReport(Array.Empty<StartupCompatibilityIssue>());

        var status = StartupCompatibilityStatusPolicy.Resolve(report);

        status.Should().Be(CompatibilityHealthStatus.Normal);
        StartupCompatibilityStatusPolicy.ToBadgeText(status).Should().Be("兼容状态：正常");
    }

    [Fact]
    public void Resolve_ShouldReturnDegraded_WhenWarningsOnly()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    Code: "warning-only",
                    Message: "warning",
                    Suggestion: "fix",
                    IsBlocking: false)
            });

        var status = StartupCompatibilityStatusPolicy.Resolve(report);

        status.Should().Be(CompatibilityHealthStatus.Degraded);
        StartupCompatibilityStatusPolicy.ToBadgeText(status).Should().Be("兼容状态：降级");
    }

    [Fact]
    public void Resolve_ShouldReturnBlocked_WhenBlockingExists()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue(
                    Code: "blocking",
                    Message: "blocking",
                    Suggestion: "fix",
                    IsBlocking: true)
            });

        var status = StartupCompatibilityStatusPolicy.Resolve(report);

        status.Should().Be(CompatibilityHealthStatus.Blocked);
        StartupCompatibilityStatusPolicy.ToBadgeText(status).Should().Be("兼容状态：阻断");
    }
}
