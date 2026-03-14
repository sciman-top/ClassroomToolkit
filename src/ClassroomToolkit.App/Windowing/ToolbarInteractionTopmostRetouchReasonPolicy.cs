namespace ClassroomToolkit.App.Windowing;

internal static class ToolbarInteractionTopmostRetouchReasonPolicy
{
    internal static string ResolveTag(ToolbarInteractionTopmostRetouchReason reason)
    {
        return reason switch
        {
            ToolbarInteractionTopmostRetouchReason.OverlayHidden => "overlay-hidden",
            ToolbarInteractionTopmostRetouchReason.SceneNotInteractive => "scene-not-interactive",
            ToolbarInteractionTopmostRetouchReason.InteractiveSceneActive => "interactive-scene-active",
            _ => "none"
        };
    }
}
