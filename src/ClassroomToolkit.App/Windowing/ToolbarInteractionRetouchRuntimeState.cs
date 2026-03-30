namespace ClassroomToolkit.App.Windowing;

internal readonly record struct ToolbarInteractionRetouchRuntimeState(
    DateTime LastRetouchUtc,
    DateTime LastPreviewMouseDownUtc)
{
    internal static ToolbarInteractionRetouchRuntimeState Default => new(
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
        LastPreviewMouseDownUtc: WindowDedupDefaults.UnsetTimestampUtc);
}
