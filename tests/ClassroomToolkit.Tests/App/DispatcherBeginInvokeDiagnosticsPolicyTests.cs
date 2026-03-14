using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class DispatcherBeginInvokeDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFailureMessage_ShouldContainOperationAndException()
    {
        var message = DispatcherBeginInvokeDiagnosticsPolicy.FormatFailureMessage(
            "FocusOverlay",
            "InvalidOperationException",
            "dispatcher shutdown");

        message.Should().Contain("[Dispatcher][BeginInvoke] failed");
        message.Should().Contain("op=FocusOverlay");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=dispatcher shutdown");
    }
}
