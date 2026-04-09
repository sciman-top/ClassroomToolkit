namespace ClassroomToolkit.App.RollCall;

internal delegate bool RollCallTryRollNextDelegate(out string? message);

internal static class RollCallRemoteHookActionExecutionCoordinator
{
    internal static void ExecuteRoll(
        bool isRollCallMode,
        RollCallTryRollNextDelegate tryRollNext,
        Action updatePhotoDisplay,
        Action speakStudentName,
        Action scheduleRollStateSave,
        Action<string> showRollCallMessage)
    {
        ArgumentNullException.ThrowIfNull(tryRollNext);
        ArgumentNullException.ThrowIfNull(updatePhotoDisplay);
        ArgumentNullException.ThrowIfNull(speakStudentName);
        ArgumentNullException.ThrowIfNull(scheduleRollStateSave);
        ArgumentNullException.ThrowIfNull(showRollCallMessage);

        if (!isRollCallMode)
        {
            return;
        }

        if (tryRollNext(out var message))
        {
            speakStudentName();
            updatePhotoDisplay();
            scheduleRollStateSave();
            return;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            showRollCallMessage(message);
        }
    }

    internal static void ExecuteGroupSwitch(
        bool isRollCallMode,
        Action switchToNextGroup,
        Action showGroupOverlay,
        Action scheduleRollStateSave)
    {
        ArgumentNullException.ThrowIfNull(switchToNextGroup);
        ArgumentNullException.ThrowIfNull(showGroupOverlay);
        ArgumentNullException.ThrowIfNull(scheduleRollStateSave);

        if (!isRollCallMode)
        {
            return;
        }

        switchToNextGroup();
        showGroupOverlay();
        scheduleRollStateSave();
    }
}
