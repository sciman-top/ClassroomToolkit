using ClassroomToolkit.Interop.Utilities;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class InteropEventDispatchPolicyTests
{
    [Fact]
    public void InvokeSafely_Action1_ShouldContinueAfterRecoverableSubscriberFailure()
    {
        var callCount = 0;
        Action<int> handlers = _ => throw new InvalidOperationException("first failed");
        handlers += _ => callCount++;

        InteropEventDispatchPolicy.InvokeSafely(handlers, 1, "test-action1");

        callCount.Should().Be(1);
    }

    [Fact]
    public void InvokeSafely_Action1_ShouldPropagateFatalSubscriberFailure()
    {
        Action<int> handlers = _ => throw new OutOfMemoryException("fatal");

        var action = () => InteropEventDispatchPolicy.InvokeSafely(handlers, 1, "test-action1-fatal");

        action.Should().Throw<OutOfMemoryException>();
    }

    [Fact]
    public void InvokeSafely_Action2_ShouldContinueAfterRecoverableSubscriberFailure()
    {
        var callCount = 0;
        Action<int, string> handlers = (_, _) => throw new InvalidOperationException("first failed");
        handlers += (_, _) => callCount++;

        InteropEventDispatchPolicy.InvokeSafely(handlers, 1, "src", "test-action2");

        callCount.Should().Be(1);
    }

    [Fact]
    public void InvokeSafely_Action2_ShouldPropagateFatalSubscriberFailure()
    {
        Action<int, string> handlers = (_, _) => throw new OutOfMemoryException("fatal");

        var action = () => InteropEventDispatchPolicy.InvokeSafely(handlers, 1, "src", "test-action2-fatal");

        action.Should().Throw<OutOfMemoryException>();
    }
}
