namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkSaveUpdateTransitionPlan(
    bool ShouldStopAutoSaveTimer,
    bool ShouldCancelPendingAutoSave,
    bool ShouldScheduleAutoSave);

internal static class InkSaveUpdateTransitionPolicy
{
    internal static InkSaveUpdateTransitionPlan Resolve(bool enabled)
    {
        return enabled
            ? new InkSaveUpdateTransitionPlan(
                ShouldStopAutoSaveTimer: false,
                ShouldCancelPendingAutoSave: false,
                ShouldScheduleAutoSave: true)
            : new InkSaveUpdateTransitionPlan(
                ShouldStopAutoSaveTimer: true,
                ShouldCancelPendingAutoSave: true,
                ShouldScheduleAutoSave: false);
    }
}
