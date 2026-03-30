using ClassroomToolkit.App.RollCall;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class RollCallRemoteHookDispatchPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void CanDispatch_ShouldMatchExpected(
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished,
        bool expected)
    {
        RollCallRemoteHookDispatchPolicy.CanDispatch(
                dispatcherShutdownStarted,
                dispatcherShutdownFinished)
            .Should()
            .Be(expected);
    }
}
