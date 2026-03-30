namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageInkVisualSyncRuntimeState(
    DateTime LastSyncUtc,
    CrossPageInkVisualSyncTrigger? LastTrigger)
{
    internal static CrossPageInkVisualSyncRuntimeState Default => new(
        LastSyncUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
        LastTrigger: null);
}
