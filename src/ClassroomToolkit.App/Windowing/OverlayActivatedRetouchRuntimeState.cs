namespace ClassroomToolkit.App.Windowing;

internal readonly record struct OverlayActivatedRetouchRuntimeState(
    bool SuppressNextApply,
    DateTime LastRetouchUtc)
{
    internal static OverlayActivatedRetouchRuntimeState Default => new(
        SuppressNextApply: false,
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc);
}
