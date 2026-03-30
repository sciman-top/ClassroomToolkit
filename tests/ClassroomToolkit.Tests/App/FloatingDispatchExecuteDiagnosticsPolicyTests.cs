using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class FloatingDispatchExecuteDiagnosticsPolicyTests
{
    [Fact]
    public void FormatFailureMessage_ShouldContainExceptionDetails()
    {
        var message = FloatingDispatchExecuteDiagnosticsPolicy.FormatFailureMessage(
            "InvalidOperationException",
            "boom");

        message.Should().Contain("[FloatingDispatchQueue][Execute] failed");
        message.Should().Contain("ex=InvalidOperationException");
        message.Should().Contain("msg=boom");
    }
}
