using AwesomeAssertions;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.Tests.Windowing;

public sealed class SafeActionExecutionExecutorTests
{
    [Fact]
    public void TryExecute_ShouldRunActionAndReturnTrue_OnSuccess()
    {
        var executed = false;

        var result = SafeActionExecutionExecutor.TryExecute(() => executed = true);

        result.Should().BeTrue();
        executed.Should().BeTrue();
    }

    [Fact]
    public void TryExecute_ShouldReturnFalseAndReportFailure_OnNonFatalException()
    {
        Exception? reported = null;
        var failure = new InvalidOperationException("callback-failed");

        var result = SafeActionExecutionExecutor.TryExecute(
            () => throw failure,
            ex => reported = ex);

        result.Should().BeFalse();
        reported.Should().BeSameAs(failure);
    }

    [Fact]
    public void TryExecute_ShouldSwallowFailureHandlerExceptions()
    {
        var result = SafeActionExecutionExecutor.TryExecute(
            () => throw new InvalidOperationException("callback-failed"),
            _ => throw new InvalidOperationException("handler-also-failed"));

        result.Should().BeFalse();
    }

    [Fact]
    public void TryExecute_ShouldPropagateFatalException()
    {
        var act = () => SafeActionExecutionExecutor.TryExecute(
            () => throw new AccessViolationException());

        act.Should().ThrowExactly<AccessViolationException>();
    }

    [Fact]
    public void TryExecuteOfT_ShouldReturnFallback_OnNonFatalException()
    {
        Exception? reported = null;

        var result = SafeActionExecutionExecutor.TryExecute(
            () => throw new InvalidOperationException("callback-failed"),
            fallback: 42,
            onFailure: ex => reported = ex);

        result.Should().Be(42);
        reported.Should().NotBeNull();
    }

    [Fact]
    public void TryExecute_ShouldThrowArgumentNullException_WhenActionIsNull()
    {
        var act = () => SafeActionExecutionExecutor.TryExecute(null!);

        act.Should().ThrowExactly<ArgumentNullException>();
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
}
