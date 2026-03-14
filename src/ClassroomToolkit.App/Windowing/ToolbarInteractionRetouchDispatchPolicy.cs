namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionRetouchDispatchMode
{
    Immediate = 0,
    Background = 1
}

internal static class ToolbarInteractionRetouchDispatchPolicy
{
    internal static ToolbarInteractionRetouchDispatchMode Resolve(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchExecutionPlan executionPlan)
    {
        if (!executionPlan.ApplyDirectDriftRepair)
        {
            return ToolbarInteractionRetouchDispatchMode.Immediate;
        }

        var interactiveScene = snapshot.OverlayVisible
                               && (snapshot.PhotoModeActive || snapshot.WhiteboardActive);
        var launcherDrift = snapshot.LauncherVisible && !snapshot.LauncherTopmost;
        if (trigger == ToolbarInteractionRetouchTrigger.Activated && interactiveScene && launcherDrift)
        {
            return ToolbarInteractionRetouchDispatchMode.Immediate;
        }

        if (trigger == ToolbarInteractionRetouchTrigger.Activated && interactiveScene)
        {
            return ToolbarInteractionRetouchDispatchMode.Background;
        }

        return ToolbarInteractionRetouchDispatchMode.Immediate;
    }
}
