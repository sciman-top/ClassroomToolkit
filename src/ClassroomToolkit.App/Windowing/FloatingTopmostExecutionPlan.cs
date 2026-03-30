namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingTopmostExecutionPlan(
    bool ToolbarTopmost,
    bool RollCallTopmost,
    bool LauncherTopmost,
    bool ImageManagerTopmost,
    bool EnforceZOrder);

internal static class FloatingTopmostExecutionPlanPolicy
{
    internal static FloatingTopmostExecutionPlan Resolve(FloatingTopmostPlan plan, bool enforceZOrder)
    {
        return new FloatingTopmostExecutionPlan(
            ToolbarTopmost: plan.ToolbarTopmost,
            RollCallTopmost: plan.RollCallTopmost,
            LauncherTopmost: plan.LauncherTopmost,
            ImageManagerTopmost: plan.ImageManagerTopmost,
            EnforceZOrder: enforceZOrder);
    }
}
