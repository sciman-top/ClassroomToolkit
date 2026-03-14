using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowActivationDiagnosticsPolicyTests
{
    [Fact]
    public void FormatExecutionSkipMessage_ShouldContainReasonTag()
    {
        var message = WindowActivationDiagnosticsPolicy.FormatExecutionSkipMessage(
            WindowActivationExecutionReason.TargetMissing);

        message.Should().Contain("[WindowActivation] skip");
        message.Should().Contain("reason=target-missing");
    }
}
