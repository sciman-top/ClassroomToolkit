using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ZOrderRequestDiagnosticsPolicyTests
{
    [Fact]
    public void FormatQueueDispatchFailedRollbackMessage_ShouldContainReasonAndForce()
    {
        var message = ZOrderRequestDiagnosticsPolicy.FormatQueueDispatchFailedRollbackMessage(forceEnforceZOrder: true);

        message.Should().Contain("[ZOrderRequest] rollback");
        message.Should().Contain("reason=queue-dispatch-failed");
        message.Should().Contain("force=True");
    }
}
