namespace ClassroomToolkit.App.Windowing;

internal readonly record struct LauncherRestoreTransitionContext(
    bool MainVisible,
    bool MainActive,
    bool BubbleVisible);
