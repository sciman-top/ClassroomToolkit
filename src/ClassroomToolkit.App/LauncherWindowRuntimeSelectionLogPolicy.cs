using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App;

internal static class LauncherWindowRuntimeSelectionLogPolicy
{
    internal static bool ShouldLog(LauncherWindowRuntimeSelectionReason reason)
    {
        return reason == LauncherWindowRuntimeSelectionReason.FallbackToMainBecauseBubbleNotVisible
               || reason == LauncherWindowRuntimeSelectionReason.FallbackToBubbleBecauseMainNotVisible;
    }
}
