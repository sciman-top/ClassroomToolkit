namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ToolbarInteractionRetouchExecutionPlan(
    bool ApplyDirectDriftRepair,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class ToolbarInteractionRetouchExecutionPlanPolicy
{
    internal static ToolbarInteractionRetouchExecutionPlan Resolve(
        ToolbarInteractionRetouchDecision decision)
    {
        if (!decision.ShouldRetouch)
        {
            return new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false);
        }

        if (decision.ForceEnforceZOrder)
        {
            return new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: decision.ForceEnforceZOrder);
        }

        return new ToolbarInteractionRetouchExecutionPlan(
            ApplyDirectDriftRepair: true,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);
    }
}
