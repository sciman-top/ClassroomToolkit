using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDeferredRefreshPolicyTests
{
    [Fact]
    public void ShouldArmOnInteractiveSwitch_ShouldReturnTrue_OnlyForDeferredMode()
    {
        CrossPageDeferredRefreshPolicy
            .ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode.DeferredByInput)
            .Should()
            .BeTrue();

        CrossPageDeferredRefreshPolicy
            .ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode.ImmediateDirect)
            .Should()
            .BeFalse();

        CrossPageDeferredRefreshPolicy
            .ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode.ImmediateScheduled)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ShouldRunOnPointerUp_ShouldMatchExpected(
        bool deferredFlag,
        bool crossPageDisplayActive,
        bool expected)
    {
        var shouldRun = CrossPageDeferredRefreshPolicy.ShouldRunOnPointerUp(
            deferredFlag,
            crossPageDisplayActive);

        shouldRun.Should().Be(expected);
    }
}
