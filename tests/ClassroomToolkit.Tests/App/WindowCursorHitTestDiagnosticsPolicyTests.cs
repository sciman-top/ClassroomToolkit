using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowCursorHitTestDiagnosticsPolicyTests
{
    [Fact]
    public void FormatResolveMessage_ShouldContainBothReasonTags()
    {
        var message = WindowCursorHitTestDiagnosticsPolicy.FormatResolveMessage(
            WindowCursorHitTestExecutionReason.HitTestCompleted,
            WindowCursorHitTestReason.InsideBounds);

        message.Should().Contain("[WindowCursorHitTest]");
        message.Should().Contain("exec=hit-test-completed");
        message.Should().Contain("hit=inside-bounds");
    }
}
