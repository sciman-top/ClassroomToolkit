namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionRetouchPath
{
    None = 0,
    DirectDriftRepair = 1,
    ZOrderApply = 2
}

internal static class ToolbarInteractionRetouchPathPolicy
{
    internal static ToolbarInteractionRetouchPath Resolve(
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchDecision decision)
    {
        if (!decision.ShouldRetouch)
        {
            return ToolbarInteractionRetouchPath.None;
        }

        if (decision.ForceEnforceZOrder)
        {
            return ToolbarInteractionRetouchPath.ZOrderApply;
        }

        var toolbarDrift = snapshot.ToolbarVisible && !snapshot.ToolbarTopmost;
        var rollCallDrift = snapshot.RollCallVisible && !snapshot.RollCallTopmost;
        var launcherDrift = snapshot.LauncherVisible && !snapshot.LauncherTopmost;

        // Keep toolbar-interaction repair on the lightweight direct path by default.
        // Centralized z-order replay is reserved for explicit force-enforce cases,
        // which helps reduce launcher flash/disappear under repeated toolbar clicks.
        if (launcherDrift || toolbarDrift || rollCallDrift)
        {
            return ToolbarInteractionRetouchPath.DirectDriftRepair;
        }

        return ToolbarInteractionRetouchPath.DirectDriftRepair;
    }
}
