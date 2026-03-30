using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchQueueDiagnosticsPolicyTests
{
    [Fact]
    public void FormatRequestDecisionMessage_ShouldContainActionReasonAndForce()
    {
        var message = FloatingDispatchQueueDiagnosticsPolicy.FormatRequestDecisionMessage(
            FloatingDispatchQueueAction.None,
            FloatingDispatchQueueReason.MergedIntoQueuedRequest,
            forceEnforceZOrder: true);

        message.Should().Contain("[FloatingDispatchQueue]");
        message.Should().Contain("action=None");
        message.Should().Contain("reason=merged-into-queued-request");
        message.Should().Contain("force=True");
    }

    [Fact]
    public void FormatQueueDispatchFailureExceptionMessage_ShouldContainExceptionTypeAndMessage()
    {
        var message = FloatingDispatchQueueDiagnosticsPolicy.FormatQueueDispatchFailureExceptionMessage(
            "InvalidOperationException",
            "boom");

        message.Should().Contain("[FloatingDispatchQueue]");
        message.Should().Contain("dispatch-failed");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=boom");
    }
}
