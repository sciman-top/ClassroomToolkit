namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SurfaceZOrderDecisionRuntimeState(
    SurfaceZOrderDecision? LastDecision,
    DateTime LastAppliedUtc)
{
    internal static SurfaceZOrderDecisionRuntimeState Default => new(
        LastDecision: null,
        LastAppliedUtc: WindowDedupDefaults.UnsetTimestampUtc);
}
