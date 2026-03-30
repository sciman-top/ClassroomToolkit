namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ZOrderRequestRuntimeState(
    DateTime LastRequestUtc,
    bool LastForceEnforceZOrder)
{
    internal static ZOrderRequestRuntimeState Default => new(
        WindowDedupDefaults.UnsetTimestampUtc,
        LastForceEnforceZOrder: false);
}
