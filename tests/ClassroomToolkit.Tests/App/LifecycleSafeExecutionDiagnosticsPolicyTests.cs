using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class LifecycleSafeExecutionDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFailureMessage_ShouldContainAllFields()
    {
        var message = LifecycleSafeExecutionDiagnosticsPolicy.FormatFailureMessage(
            phase: "request-exit",
            operation: "shutdown-application",
            exceptionType: "InvalidOperationException",
            message: "boom");

        message.Should().Contain("[LifecycleSafeExecution] failed");
        message.Should().Contain("phase=request-exit");
        message.Should().Contain("op=shutdown-application");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=boom");
    }
}
