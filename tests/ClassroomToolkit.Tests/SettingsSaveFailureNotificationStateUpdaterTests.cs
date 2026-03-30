using ClassroomToolkit.App;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class SettingsSaveFailureNotificationStateUpdaterTests
{
    [Fact]
    public void MarkSaveSucceeded_ShouldClearNotificationFlag()
    {
        var saveFailedNotified = true;

        SettingsSaveFailureNotificationStateUpdater.MarkSaveSucceeded(ref saveFailedNotified);

        saveFailedNotified.Should().BeFalse();
    }

    [Fact]
    public void ApplyNotificationPlan_ShouldPersistNextNotificationState()
    {
        var saveFailedNotified = false;
        var plan = SettingsSaveFailureNotificationPolicy.Resolve(alreadyNotified: saveFailedNotified);

        SettingsSaveFailureNotificationStateUpdater.ApplyNotificationPlan(
            ref saveFailedNotified,
            plan);

        saveFailedNotified.Should().BeTrue();
    }
}
