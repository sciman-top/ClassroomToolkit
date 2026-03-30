namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionDecisionStateUpdater
{
    internal static void Apply(
        ref FloatingCoordinationRuntimeState runtimeState,
        FloatingWindowCoordinationState state)
    {
        runtimeState = new FloatingCoordinationRuntimeState(
            LastFrontSurface: state.LastFrontSurface,
            LastTopmostPlan: state.LastTopmostPlan);
    }

    internal static void Apply(
        ref ZOrderSurface? lastFrontSurface,
        ref FloatingTopmostPlan? lastTopmostPlan,
        FloatingWindowCoordinationState state)
    {
        lastFrontSurface = state.LastFrontSurface;
        lastTopmostPlan = state.LastTopmostPlan;
    }
}
