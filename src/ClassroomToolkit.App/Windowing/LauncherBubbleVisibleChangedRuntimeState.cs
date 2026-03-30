namespace ClassroomToolkit.App.Windowing;

internal readonly record struct LauncherBubbleVisibleChangedRuntimeState(
    bool? LastVisibleState,
    DateTime LastEventUtc)
{
    internal static LauncherBubbleVisibleChangedRuntimeState Default => new(
        LastVisibleState: null,
        LastEventUtc: WindowDedupDefaults.UnsetTimestampUtc);
}
