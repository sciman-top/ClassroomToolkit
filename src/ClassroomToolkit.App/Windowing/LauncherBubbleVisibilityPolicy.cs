namespace ClassroomToolkit.App.Windowing;

internal readonly record struct LauncherBubbleVisibilityDecision(
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class LauncherBubbleVisibilityPolicy
{
    internal static LauncherBubbleVisibilityDecision Resolve(bool bubbleVisible)
    {
        return new LauncherBubbleVisibilityDecision(
            RequestZOrderApply: bubbleVisible,
            ForceEnforceZOrder: bubbleVisible);
    }
}
