namespace ClassroomToolkit.App.Windowing;

internal readonly record struct LauncherWindowRuntimeSnapshot(
    bool VisibleForTopmost,
    bool Active,
    LauncherWindowKind WindowKind,
    LauncherWindowRuntimeSelectionReason SelectionReason);

internal enum LauncherWindowRuntimeSelectionReason
{
    None = 0,
    PreferMainVisible = 1,
    PreferBubbleVisible = 2,
    FallbackToMainBecauseBubbleNotVisible = 3,
    FallbackToBubbleBecauseMainNotVisible = 4
}
