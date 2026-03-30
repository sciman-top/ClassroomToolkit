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

    [Fact]
    public void TryExecute_ShouldRethrowFatalException_WhenActionThrowsFatal()
    {
        var act = () => SafeActionExecutionExecutor.TryExecute(
            () => throw new BadImageFormatException("fatal"));

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void TryExecute_ShouldRethrowFatalException_WhenFailureCallbackThrowsFatal()
    {
        var act = () => SafeActionExecutionExecutor.TryExecute(
            () => throw new InvalidOperationException("boom"),
            _ => throw new BadImageFormatException("fatal-callback"));

        act.Should().Throw<BadImageFormatException>();
    }

    [Fact]
    public void TryExecuteOfT_ShouldReturnValue_WhenFuncSucceeds()
    {
        var result = SafeActionExecutionExecutor.TryExecute(() => 42, fallback: -1);

        result.Should().Be(42);
    }

    [Fact]
    public void TryExecuteOfT_ShouldReturnFallbackAndInvokeFailure_WhenFuncThrows()
    {
        var captured = default(Exception);

        var result = SafeActionExecutionExecutor.TryExecute(
            () => throw new InvalidOperationException("boom"),
            fallback: -1,
            onFailure: ex => captured = ex);

        result.Should().Be(-1);
        captured.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public void TryExecuteOfT_ShouldRethrowFatalException_WhenFuncThrowsFatal()
    {
        var act = () => SafeActionExecutionExecutor.TryExecute(
            () => throw new BadImageFormatException("fatal"),
            fallback: false);

        act.Should().Throw<BadImageFormatException>();
    }
}
