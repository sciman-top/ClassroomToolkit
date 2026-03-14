using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowInteropRetryExecutorTests
{
    [Fact]
    public void Execute_ShouldReturnTrue_WhenSecondAttemptSucceeds()
    {
        var callCount = 0;

        var result = WindowInteropRetryExecutor.Execute(
            _ =>
            {
                callCount++;
                return callCount == 2 ? (true, 0) : (false, 5);
            },
            (_, errorCode) => errorCode == 5);

        result.Should().BeTrue();
        callCount.Should().Be(2);
    }

    [Fact]
    public void Execute_ShouldReturnFalse_WhenRetryPolicyRejects()
    {
        var callCount = 0;

        var result = WindowInteropRetryExecutor.Execute(
            _ =>
            {
                callCount++;
                return (false, 1400);
            },
            (_, errorCode) => errorCode == 5);

        result.Should().BeFalse();
        callCount.Should().Be(1);
    }

    [Fact]
    public void ExecuteWithValue_ShouldReturnResolvedValue_WhenRetrySucceeds()
    {
        var callCount = 0;

        var result = WindowInteropRetryExecutor.ExecuteWithValue(
            _ =>
            {
                callCount++;
                return callCount == 2
                    ? (true, 42, 0)
                    : (false, 0, 5);
            },
            (_, errorCode) => errorCode == 5,
            out var value);

        result.Should().BeTrue();
        value.Should().Be(42);
        callCount.Should().Be(2);
    }
}
