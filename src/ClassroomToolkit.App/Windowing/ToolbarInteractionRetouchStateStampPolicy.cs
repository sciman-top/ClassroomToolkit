namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchStateStampPolicy
{
    internal static bool ShouldMarkRetouched(
        ToolbarInteractionRetouchTrigger trigger,
        ToolbarInteractionRetouchExecutionPlan executionPlan)
    {
        if (trigger == ToolbarInteractionRetouchTrigger.PreviewMouseDown
            && executionPlan.RequestZOrderApply
            && executionPlan.ForceEnforceZOrder)
        {
            return false;
        }

        return executionPlan.ApplyDirectDriftRepair
            || executionPlan.RequestZOrderApply;
    }
}
