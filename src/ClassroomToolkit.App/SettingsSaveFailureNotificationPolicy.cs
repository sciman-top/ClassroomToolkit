namespace ClassroomToolkit.App;

internal readonly record struct SettingsSaveFailureNotificationPlan(
    bool ShouldNotify,
    bool NextNotifiedState);

internal static class SettingsSaveFailureNotificationPolicy
{
    internal static SettingsSaveFailureNotificationPlan Resolve(bool alreadyNotified)
    {
        if (alreadyNotified)
        {
            return new SettingsSaveFailureNotificationPlan(
                ShouldNotify: false,
                NextNotifiedState: true);
        }

        return new SettingsSaveFailureNotificationPlan(
            ShouldNotify: true,
            NextNotifiedState: true);
    }
}
