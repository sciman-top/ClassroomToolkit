namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ToolbarInteractionRetouchExecutionPlan(
    bool ApplyDirectDriftRepair,
    bool RequestZOrderApply,
    bool ForceEnforceZOrder);

internal static class ToolbarInteractionRetouchExecutionPlanPolicy
{
    internal static ToolbarInteractionRetouchExecutionPlan Resolve(
        ToolbarInteractionRetouchSnapshot snapshot,
        ToolbarInteractionRetouchDecision decision)
    {
        if (!decision.ShouldRetouch)
        {
            return new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false);
        }

        var path = ToolbarInteractionRetouchPathPolicy.Resolve(snapshot, decision);
        if (path == ToolbarInteractionRetouchPath.ZOrderApply)
        {
            return new ToolbarInteractionRetouchExecutionPlan(
                ApplyDirectDriftRepair: false,
                RequestZOrderApply: true,
                ForceEnforceZOrder: decision.ForceEnforceZOrder);
        }

        return new ToolbarInteractionRetouchExecutionPlan(
            ApplyDirectDriftRepair: path == ToolbarInteractionRetouchPath.DirectDriftRepair,
            RequestZOrderApply: false,
            ForceEnforceZOrder: false);
    }
}
