namespace ClassroomToolkit.App.Windowing;

internal static class OverlayActivationSuppressionPolicyAdapter
{
    internal static FloatingWindowActivationPlan ApplySuppression(
        FloatingWindowActivationPlan plan,
        bool suppressOverlayActivation)
    {
        if (!suppressOverlayActivation)
        {
            return plan;
        }

        return plan with { ActivateOverlay = false };
    }
}
