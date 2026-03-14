using System;

namespace ClassroomToolkit.App.Windowing;

internal static class SurfaceZOrderDecisionStateUpdater
{
    internal static void Apply(
        ref SurfaceZOrderDecisionRuntimeState state,
        SurfaceZOrderDecisionDedupDecision dedupDecision)
    {
        state = new SurfaceZOrderDecisionRuntimeState(
            LastDecision: dedupDecision.LastDecision,
            LastAppliedUtc: dedupDecision.LastAppliedUtc);
    }

    internal static void Apply(
        ref SurfaceZOrderDecision? lastDecision,
        ref DateTime lastAppliedUtc,
        SurfaceZOrderDecisionDedupDecision dedupDecision)
    {
        lastDecision = dedupDecision.LastDecision;
        lastAppliedUtc = dedupDecision.LastAppliedUtc;
    }
}
