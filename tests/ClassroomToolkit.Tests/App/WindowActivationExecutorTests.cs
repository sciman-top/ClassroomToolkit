using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowActivationExecutorTests
{
    [Fact]
    public void ResolveExecution_ShouldReturnExecuted_WhenRequested_AndTargetProvided()
    {
        var decision = WindowActivationExecutor.ResolveExecution(
            target: "target",
            shouldExecute: true);

        decision.ShouldExecute.Should().BeTrue();
        decision.Reason.Should().Be(WindowActivationExecutionReason.Executed);
    }

    [Fact]
    public void TryExecute_ShouldInvokeAction_WhenRequested_AndTargetProvided()
    {
        var called = false;

        var result = WindowActivationExecutor.TryExecute(
            target: "target",
            shouldExecute: true,
            executeAction: _ => called = true);

        result.Should().BeTrue();
        called.Should().BeTrue();
    }

    [Fact]
    public void ResolveExecution_ShouldReturnExecutionNotRequested_WhenExecutionNotRequested()
    {
        var decision = WindowActivationExecutor.ResolveExecution(
            target: "target",
            shouldExecute: false);

        decision.ShouldExecute.Should().BeFalse();
        decision.Reason.Should().Be(WindowActivationExecutionReason.ExecutionNotRequested);
    }

    [Fact]
    public void TryExecute_ShouldReturnFalse_WhenExecutionNotRequested()
    {
        var called = false;

        var result = WindowActivationExecutor.TryExecute(
            target: "target",
            shouldExecute: false,
            executeAction: _ => called = true);

        result.Should().BeFalse();
        called.Should().BeFalse();
    }

    [Fact]
    public void ResolveExecution_ShouldReturnTargetMissing_WhenTargetIsNull()
    {
        var decision = WindowActivationExecutor.ResolveExecution<string>(
            target: null,
            shouldExecute: true);

        decision.ShouldExecute.Should().BeFalse();
        decision.Reason.Should().Be(WindowActivationExecutionReason.TargetMissing);
    }

    [Fact]
    public void TryExecute_ShouldReturnFalse_WhenTargetIsNull()
    {
        var called = false;

        var result = WindowActivationExecutor.TryExecute<string>(
            target: null,
            shouldExecute: true,
            executeAction: _ => called = true);

        result.Should().BeFalse();
        called.Should().BeFalse();
    }
}
