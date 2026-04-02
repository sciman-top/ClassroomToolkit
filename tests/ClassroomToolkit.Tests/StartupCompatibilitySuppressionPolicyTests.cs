using ClassroomToolkit.App.Diagnostics;
using ClassroomToolkit.Services.Compatibility;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

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

        var filtered = StartupCompatibilitySuppressionPolicy.FilterWarnings(report, new[] { "warn-a" });

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

        var merged = StartupCompatibilitySuppressionPolicy.MergeSuppressedWarningCodes(new[] { "legacy" }, report);

        merged.Should().BeEquivalentTo(new[] { "legacy", "warn-a" });
    }
}
