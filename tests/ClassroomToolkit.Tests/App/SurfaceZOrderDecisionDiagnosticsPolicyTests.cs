using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SurfaceZOrderDecisionDiagnosticsPolicyTests
{
    [Fact]
    public void FormatDedupSkipMessage_ShouldContainReasonTag()
    {
        var message = SurfaceZOrderDecisionDiagnosticsPolicy.FormatDedupSkipMessage(
            SurfaceZOrderDecisionDedupReason.SkippedWithinDedupWindow);

        message.Should().Contain("[SurfaceZOrder][Dedup] skip");
        message.Should().Contain("reason=skipped-within-window");
    }
}
