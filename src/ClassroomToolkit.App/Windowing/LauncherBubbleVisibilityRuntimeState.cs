using System;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct LauncherBubbleVisibilityRuntimeState(
    bool SuppressVisibleChangedApply,
    DateTime SuppressVisibleChangedUntilUtc,
    LauncherBubbleVisibleChangedRuntimeState VisibleChangedState)
{
    internal static LauncherBubbleVisibilityRuntimeState Default => new(
        SuppressVisibleChangedApply: false,
        SuppressVisibleChangedUntilUtc: WindowDedupDefaults.UnsetTimestampUtc,
        VisibleChangedState: LauncherBubbleVisibleChangedRuntimeState.Default);
}
