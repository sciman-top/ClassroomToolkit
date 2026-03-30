using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class BorderFixDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFailureMessage_ShouldContainPhaseTargetAndException()
    {
        var message = BorderFixDiagnosticsPolicy.FormatFailureMessage(
            "paint-settings",
            "paint-settings-dialog",
            "InvalidOperationException",
            "boom");

        message.Should().Contain("[BorderFix] failed");
        message.Should().Contain("phase=paint-settings");
        message.Should().Contain("target=paint-settings-dialog");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=boom");
    }
}
