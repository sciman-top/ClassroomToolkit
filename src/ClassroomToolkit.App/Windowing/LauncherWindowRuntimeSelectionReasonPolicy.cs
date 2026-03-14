namespace ClassroomToolkit.App.Windowing;

internal static class LauncherWindowRuntimeSelectionReasonPolicy
{
    internal static string ResolveTag(LauncherWindowRuntimeSelectionReason reason)
    {
        return reason switch
        {
            LauncherWindowRuntimeSelectionReason.PreferMainVisible => "prefer-main-visible",
            LauncherWindowRuntimeSelectionReason.PreferBubbleVisible => "prefer-bubble-visible",
            LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible => "fallback-main-bubble-hidden",
            LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible => "fallback-bubble-main-hidden",
            _ => "none"
        };
    }
}
