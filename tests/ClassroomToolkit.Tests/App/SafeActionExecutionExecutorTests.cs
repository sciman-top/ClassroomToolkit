using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class SafeActionExecutionExecutorTests
{
    [Fact]
    public void TryExecute_ShouldReturnTrue_WhenActionSucceeds()
    {
        var invoked = false;

        var result = SafeActionExecutionExecutor.TryExecute(() => invoked = true);

        result.Should().BeTrue();
        invoked.Should().BeTrue();
    }

    [Fact]
    public void TryExecute_ShouldReturnFalseAndInvokeFailure_WhenActionThrows()
    {
        var captured = default(Exception);

        var result = SafeActionExecutionExecutor.TryExecute(
            () => throw new InvalidOperationException("boom"),
            ex => captured = ex);

        result.Should().BeFalse();
        captured.Should().BeOfType<InvalidOperationException>();
        captured!.Message.Should().Be("boom");
    }

    [Fact]
    public void TryExecute_ShouldSwallowFailureCallbackException()
    {
        var result = SafeActionExecutionExecutor.TryExecute(
            () => throw new InvalidOperationException("boom"),
            _ => throw new ApplicationException("callback-failed"));

        result.Should().BeFalse();
    }
}
