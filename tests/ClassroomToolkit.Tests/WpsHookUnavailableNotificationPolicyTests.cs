using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrueOnlyFirstTime()
    {
        var state = 0;

        var first = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);
        var second = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);

        first.Should().BeTrue();
        second.Should().BeFalse();
        WpsHookUnavailableNotificationPolicy.IsNotified(ref state).Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldAllowNotifyAgain()
    {
        var state = 0;
        _ = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);

        WpsHookUnavailableNotificationPolicy.Reset(ref state);
        var afterReset = WpsHookUnavailableNotificationPolicy.ShouldNotify(ref state);

        afterReset.Should().BeTrue();
    }
}
