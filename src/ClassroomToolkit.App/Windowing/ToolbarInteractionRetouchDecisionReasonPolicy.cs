namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionRetouchDecisionReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionRetouchDecisionReason reason)
    {
        return reason switch
        {
            ToolbarInteractionRetouchDecisionReason.PreviewMouseDown => "preview-mousedown",
            ToolbarInteractionRetouchDecisionReason.SceneNotInteractive => "scene-not-interactive",
            ToolbarInteractionRetouchDecisionReason.NoTopmostDrift => "no-topmost-drift",
            _ => "retouch"
        };
    }
}
