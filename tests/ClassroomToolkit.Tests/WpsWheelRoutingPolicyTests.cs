using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsWheelRoutingPolicyTests
{
    [Fact]
    public void ShouldBypassDirectSend_ShouldReturnTrue_WhenWpsHookIsBlocking()
    {
        var result = WpsWheelRoutingPolicy.ShouldBypassDirectSend(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: true,
            isWpsForeground: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBypassDirectSend_ShouldReturnFalse_WhenHookNotBlocking()
    {
        var result = WpsWheelRoutingPolicy.ShouldBypassDirectSend(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: false,
            isWpsForeground: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldBypassDirectSend_ShouldReturnFalse_WhenForegroundNotWps()
    {
        var result = WpsWheelRoutingPolicy.ShouldBypassDirectSend(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: true,
            isWpsForeground: false);

        result.Should().BeFalse();
    }
}
