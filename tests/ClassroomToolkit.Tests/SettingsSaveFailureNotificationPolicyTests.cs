using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class SettingsSaveFailureNotificationPolicyTests
{
    [Fact]
    public void Resolve_ShouldNotify_WhenNotPreviouslyNotified()
    {
        var plan = SettingsSaveFailureNotificationPolicy.Resolve(alreadyNotified: false);

        plan.ShouldNotify.Should().BeTrue();
        plan.NextNotifiedState.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldNotNotify_WhenAlreadyNotified()
    {
        var plan = SettingsSaveFailureNotificationPolicy.Resolve(alreadyNotified: true);

        plan.ShouldNotify.Should().BeFalse();
        plan.NextNotifiedState.Should().BeTrue();
    }
}
