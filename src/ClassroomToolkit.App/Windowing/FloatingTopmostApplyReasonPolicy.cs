namespace ClassroomToolkit.App.Windowing;

internal static class FloatingTopmostApplyReasonPolicy
{
    internal static string ResolveTag(FloatingTopmostApplyPolicy.FloatingTopmostApplyReason reason)
    {
        return reason switch
        {
            FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.ForceRequested => "force-requested",
            FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.MissingLastState => "missing-last-state",
            FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.FrontSurfaceChanged => "front-surface-changed",
            FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.TopmostPlanChanged => "topmost-plan-changed",
            FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.LauncherInteractiveRetouch => "launcher-interactive-retouch",
            FloatingTopmostApplyPolicy.FloatingTopmostApplyReason.Unchanged => "unchanged",
            _ => "none"
        };
    }
}
