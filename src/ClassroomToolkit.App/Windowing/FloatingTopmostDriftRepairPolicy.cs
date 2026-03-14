namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingTopmostDriftRepairPlan(
    bool RepairToolbar,
    bool RepairRollCall,
    bool RepairLauncher);

internal static class FloatingTopmostDriftRepairPolicy
{
    internal static FloatingTopmostDriftRepairPlan Resolve(ToolbarInteractionRetouchSnapshot snapshot)
    {
        return new FloatingTopmostDriftRepairPlan(
            RepairToolbar: snapshot.ToolbarVisible && !snapshot.ToolbarTopmost,
            RepairRollCall: snapshot.RollCallVisible && !snapshot.RollCallTopmost,
            RepairLauncher: snapshot.LauncherVisible && !snapshot.LauncherTopmost);
    }
}
