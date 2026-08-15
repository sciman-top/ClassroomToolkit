namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionRetouchRuntimeResetReason
{
    None = 0,
    OverlayClosed = 1,
    ToolbarClosed = 2,
    PaintHidden = 3,
    RequestExit = 4
}

internal readonly record struct ToolbarInteractionRetouchRuntimeState(
    DateTime LastRetouchUtc,
    DateTime LastPreviewMouseDownUtc)
{
    internal static ToolbarInteractionRetouchRuntimeState Default => new(
        LastRetouchUtc: WindowDedupDefaults.UnsetTimestampUtc,
        LastPreviewMouseDownUtc: WindowDedupDefaults.UnsetTimestampUtc);
}
