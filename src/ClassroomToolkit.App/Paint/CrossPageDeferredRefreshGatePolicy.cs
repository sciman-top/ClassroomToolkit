namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDeferredRefreshGateDecision(
    bool ShouldProceed,
    string? Reason);

internal static class CrossPageDeferredRefreshGatePolicy
{
    internal static CrossPageDeferredRefreshGateDecision ResolveBeforeSchedule(
        bool crossPageDisplayActive,
        bool interactionActive)
    {
        if (!crossPageDisplayActive)
        {
            return new CrossPageDeferredRefreshGateDecision(
                ShouldProceed: false,
                Reason: CrossPageDeferredDiagnosticReason.Inactive);
        }

        if (interactionActive)
        {
            return new CrossPageDeferredRefreshGateDecision(
                ShouldProceed: false,
                Reason: CrossPageDeferredDiagnosticReason.InteractionActive);
        }

        return new CrossPageDeferredRefreshGateDecision(
            ShouldProceed: true,
            Reason: null);
    }

    internal static CrossPageDeferredRefreshGateDecision ResolveBeforeDelayedDispatch(
        bool crossPageDisplayActive,
        bool interactionActive)
    {
        if (!crossPageDisplayActive || interactionActive)
        {
            return new CrossPageDeferredRefreshGateDecision(
                ShouldProceed: false,
                Reason: CrossPageDeferredDiagnosticReason.InactiveOrInteractionActive);
        }

        return new CrossPageDeferredRefreshGateDecision(
            ShouldProceed: true,
            Reason: null);
    }
}
