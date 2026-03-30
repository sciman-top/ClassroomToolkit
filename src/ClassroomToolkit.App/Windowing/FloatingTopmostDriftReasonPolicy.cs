namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostDriftReasonPolicy
{
    internal static string ResolveTag(FloatingTopmostDriftReason reason)
    {
        return reason switch
        {
            FloatingTopmostDriftReason.ToolbarDrift => "toolbar-drift",
            FloatingTopmostDriftReason.RollCallDrift => "rollcall-drift",
            FloatingTopmostDriftReason.LauncherDrift => "launcher-drift",
            FloatingTopmostDriftReason.NoDrift => "no-drift",
            _ => "none"
        };
    }
}
