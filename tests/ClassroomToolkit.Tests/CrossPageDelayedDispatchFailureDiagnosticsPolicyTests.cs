using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDelayedDispatchFailureDiagnosticsPolicyTests
{
    [Fact]
    public void FormatDelayFailureDetail_ShouldContainExceptionType()
    {
        var detail = CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatDelayFailureDetail(
            "TaskCanceledException");

        detail.Should().Be("delayed-delay-failed ex=TaskCanceledException");
    }

    [Fact]
    public void FormatInlineRecoveryDetail_ShouldReturnRecoveredTag_WhenTokenMatched()
    {
        var detail = CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatInlineRecoveryDetail(tokenMatched: true);

        detail.Should().Be("delayed-delay-failed-inline-recovered");
    }

    [Fact]
    public void FormatInlineRecoveryDetail_ShouldReturnSkipTag_WhenTokenMismatched()
    {
        var detail = CrossPageDelayedDispatchFailureDiagnosticsPolicy.FormatInlineRecoveryDetail(tokenMatched: false);

        detail.Should().Be("delayed-delay-failed-inline-skip-token-mismatch");
    }
}
