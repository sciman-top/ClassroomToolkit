using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class InkStartupCleanupLogPolicyTests
{
    [Fact]
    public void ShouldLogDeletionSummary_ShouldReturnTrue_WhenAnyCountPositive()
    {
        var summary = new InkStartupCleanupSummary(TotalSidecars: 1, TotalComposites: 0);

        InkStartupCleanupLogPolicy.ShouldLogDeletionSummary(summary).Should().BeTrue();
    }

    [Fact]
    public void ShouldLogDeletionSummary_ShouldReturnFalse_WhenCountsAreZero()
    {
        var summary = new InkStartupCleanupSummary(TotalSidecars: 0, TotalComposites: 0);

        InkStartupCleanupLogPolicy.ShouldLogDeletionSummary(summary).Should().BeFalse();
    }

    [Fact]
    public void FormatDeletionSummary_ShouldContainCounts()
    {
        var summary = new InkStartupCleanupSummary(TotalSidecars: 3, TotalComposites: 2);

        var message = InkStartupCleanupLogPolicy.FormatDeletionSummary(summary);

        message.Should().Contain("sidecars=3");
        message.Should().Contain("composites=2");
    }
}
