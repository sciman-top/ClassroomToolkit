namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionRetouchRuntimeResetReason
{
    None = 0,
    OverlayClosed = 1,
    ToolbarClosed = 2,
    PaintHidden = 3,
    RequestExit = 4
}

internal static class ToolbarInteractionRetouchRuntimeResetReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionRetouchRuntimeResetReason reason)
    {
        return reason switch
        {
            ToolbarInteractionRetouchRuntimeResetReason.OverlayClosed => "overlay-closed",
            ToolbarInteractionRetouchRuntimeResetReason.ToolbarClosed => "toolbar-closed",
            ToolbarInteractionRetouchRuntimeResetReason.PaintHidden => "paint-hidden",
            ToolbarInteractionRetouchRuntimeResetReason.RequestExit => "request-exit",
            _ => "none"
        };
    }
}
