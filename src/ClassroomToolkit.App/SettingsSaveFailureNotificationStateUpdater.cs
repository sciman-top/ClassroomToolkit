namespace ClassroomToolkit.App;

internal static class SettingsSaveFailureNotificationStateUpdater
{
    internal static void MarkSaveSucceeded(ref bool saveFailedNotified)
    {
        saveFailedNotified = false;
    }

    internal static void ApplyNotificationPlan(
        ref bool saveFailedNotified,
        SettingsSaveFailureNotificationPlan plan)
    {
        saveFailedNotified = plan.NextNotifiedState;
    }
}
