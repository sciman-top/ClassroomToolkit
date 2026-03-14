namespace ClassroomToolkit.App.Windowing;

internal readonly record struct FloatingWindowExecutionPlan(
    FloatingTopmostExecutionPlan TopmostExecutionPlan,
    FloatingWindowActivationPlan ActivationPlan,
    FloatingOwnerExecutionPlan OwnerPlan);

internal static class FloatingWindowExecutionPlanPolicy
{
    public static FloatingWindowExecutionPlan Resolve(
        FloatingWindowRuntimeSnapshot runtimeSnapshot,
        FloatingTopmostPlan topmostPlan,
        bool enforceZOrder,
        FloatingUtilityActivitySnapshot utilityActivity,
        FloatingOwnerRuntimeSnapshot ownerSnapshot,
        bool suppressOverlayActivation)
    {
        var topmostExecutionPlan = FloatingTopmostExecutionPlanPolicy.Resolve(topmostPlan, enforceZOrder);
        var activationSnapshot = FloatingWindowActivationSnapshotPolicy.Resolve(
            runtimeSnapshot,
            topmostPlan,
            utilityActivity);
        var activationPlan = FloatingWindowActivationPolicy.Resolve(activationSnapshot);
        var finalActivationPlan = OverlayActivationSuppressionPolicyAdapter.ApplySuppression(
            activationPlan,
            suppressOverlayActivation);

        return new FloatingWindowExecutionPlan(
            TopmostExecutionPlan: topmostExecutionPlan,
            ActivationPlan: finalActivationPlan,
            OwnerPlan: FloatingOwnerExecutionPlanPolicy.Resolve(ownerSnapshot));
    }
}
