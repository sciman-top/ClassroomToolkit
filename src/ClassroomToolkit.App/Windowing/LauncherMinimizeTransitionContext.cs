namespace ClassroomToolkit.App.Windowing;

internal readonly record struct LauncherMinimizeTransitionContext(
    bool MainVisible,
    bool BubbleVisible);
