using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LauncherDragDiagnosticsPolicyTests
{
    [Fact]
    public void FormatDragMoveFailureMessage_ShouldContainExceptionAndMessage()
    {
        var message = LauncherDragDiagnosticsPolicy.FormatDragMoveFailureMessage(
            "InvalidOperationException",
            "drag failed");

        message.Should().Contain("[LauncherDrag] drag-move-failed");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=drag failed");
    }
}
