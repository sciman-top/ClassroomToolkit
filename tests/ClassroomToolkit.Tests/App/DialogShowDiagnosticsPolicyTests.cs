using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class DialogShowDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFailureMessage_ShouldContainDialogAndMessage()
    {
        var message = DialogShowDiagnosticsPolicy.FormatFailureMessage(
            "AutoExitDialog",
            "boom");

        message.Should().Contain("[DialogShow] failed");
        message.Should().Contain("dialog=AutoExitDialog");
        message.Should().Contain("msg=boom");
    }
}
