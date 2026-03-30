namespace ClassroomToolkit.App.Paint;

internal static class CrossPageInkVisualSyncStateUpdater
{
    internal static void MarkApplied(
        ref CrossPageInkVisualSyncRuntimeState state,
        DateTime nowUtc,
        CrossPageInkVisualSyncTrigger trigger)
    {
        state = new CrossPageInkVisualSyncRuntimeState(
            LastSyncUtc: nowUtc,
            LastTrigger: trigger);
    }
}
