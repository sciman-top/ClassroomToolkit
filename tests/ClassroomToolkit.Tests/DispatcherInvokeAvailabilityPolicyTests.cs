using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class DispatcherInvokeAvailabilityPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void CanBeginInvoke_ShouldMatchExpected(
        bool hasShutdownStarted,
        bool hasShutdownFinished,
        bool expected)
    {
        DispatcherInvokeAvailabilityPolicy.CanBeginInvoke(
                hasShutdownStarted,
                hasShutdownFinished)
            .Should()
            .Be(expected);
    }
}
