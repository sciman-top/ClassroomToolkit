using AwesomeAssertions;
using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.Services.Compatibility;

namespace ClassroomToolkit.Tests;

public sealed class StartupCompatibilityStatusPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNormal_WhenNoIssues()
    {
        var report = new StartupCompatibilityReport(Array.Empty<StartupCompatibilityIssue>());

        var status = StartupDiagnosticsPolicies.Resolve(report);

        status.Should().Be(CompatibilityHealthStatus.Normal);
        StartupDiagnosticsPolicies.ToBadgeText(status).Should().Be("兼容状态：正常");
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

        var status = StartupDiagnosticsPolicies.Resolve(report);

        status.Should().Be(CompatibilityHealthStatus.Degraded);
        StartupDiagnosticsPolicies.ToBadgeText(status).Should().Be("兼容状态：降级");
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

        var status = StartupDiagnosticsPolicies.Resolve(report);

        status.Should().Be(CompatibilityHealthStatus.Blocked);
        StartupDiagnosticsPolicies.ToBadgeText(status).Should().Be("兼容状态：阻断");
    }
}

public sealed class StartupCompatibilitySuppressionPolicyTests
{
    [Fact]
    public void FilterWarnings_ShouldKeepBlockingIssues_AndRemoveSuppressedWarnings()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue("warn-a", "A", "SA", false),
                new StartupCompatibilityIssue("warn-b", "B", "SB", false),
                new StartupCompatibilityIssue("block-a", "C", "SC", true)
            });

        var filtered = StartupDiagnosticsPolicies.FilterWarnings(report, new[] { "warn-a" });

        filtered.Issues.Select(x => x.Code).Should().Equal("warn-b", "block-a");
    }

    [Fact]
    public void MergeSuppressedWarningCodes_ShouldOnlyAddWarningCodes()
    {
        var report = new StartupCompatibilityReport(
            new[]
            {
                new StartupCompatibilityIssue("warn-a", "A", "SA", false),
                new StartupCompatibilityIssue("block-a", "B", "SB", true)
            });

        var merged = StartupDiagnosticsPolicies.MergeSuppressedWarningCodes(new[] { "legacy" }, report);

        merged.Should().BeEquivalentTo(new[] { "legacy", "warn-a" });
    }
}

public sealed class StartupDiagnosticsGatePolicyTests
{
    [Fact]
    public void ShouldRun_ShouldReturnFalse_WhenFlagIsOne()
    {
        StartupDiagnosticsPolicies.ShouldRun("1").Should().BeFalse();
    }

    [Fact]
    public void ShouldRun_ShouldReturnFalse_WhenFlagIsOneWithSpaces()
    {
        StartupDiagnosticsPolicies.ShouldRun(" 1 ").Should().BeFalse();
    }

    [Fact]
    public void ShouldRun_ShouldReturnTrue_WhenFlagIsNullOrOther()
    {
        StartupDiagnosticsPolicies.ShouldRun(null).Should().BeTrue();
        StartupDiagnosticsPolicies.ShouldRun(string.Empty).Should().BeTrue();
        StartupDiagnosticsPolicies.ShouldRun("0").Should().BeTrue();
        StartupDiagnosticsPolicies.ShouldRun("false").Should().BeTrue();
    }
}
