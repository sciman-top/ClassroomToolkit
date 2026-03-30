using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingWindowDiagnosticsPolicyTests
{
    [Fact]
    public void FormatExecutionSkipMessage_ShouldIncludeReasonTag()
    {
        var message = FloatingWindowDiagnosticsPolicy.FormatExecutionSkipMessage(
            FloatingWindowExecutionSkipReason.NoExecutionIntent);

        message.Should().Be("[FloatingWindow][Execution] skip reason=no-execution-intent");
    }

    [Fact]
    public void FormatActivationSkipMessage_ShouldIncludeTargetAndReason()
    {
        var message = FloatingWindowDiagnosticsPolicy.FormatActivationSkipMessage(
            "Overlay",
            FloatingActivationExecutionReason.TargetMissing);

        message.Should().Be("[FloatingWindow][Activation] skip target=Overlay reason=target-missing");
    }

    [Fact]
    public void FormatActivationAttemptFailedMessage_ShouldIncludeTarget()
    {
        var message = FloatingWindowDiagnosticsPolicy.FormatActivationAttemptFailedMessage("Overlay");

        message.Should().Be("[FloatingWindow][Activation] attempt-failed target=Overlay");
    }
}
