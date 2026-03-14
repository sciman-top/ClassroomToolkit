namespace ClassroomToolkit.App.Windowing;

internal enum ToolbarInteractionTopmostRetouchReason
{
    None = 0,
    OverlayHidden = 1,
    SceneNotInteractive = 2,
    InteractiveSceneActive = 3
}

internal readonly record struct ToolbarInteractionTopmostRetouchDecision(
    bool ShouldRetouch,
    ToolbarInteractionTopmostRetouchReason Reason);

internal static class ToolbarInteractionTopmostRetouchPolicy
{
    internal static ToolbarInteractionTopmostRetouchDecision Resolve(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive)
    {
        if (!overlayVisible)
        {
            return new ToolbarInteractionTopmostRetouchDecision(
                ShouldRetouch: false,
                Reason: ToolbarInteractionTopmostRetouchReason.OverlayHidden);
        }

        if (!(photoModeActive || whiteboardActive))
        {
            return new ToolbarInteractionTopmostRetouchDecision(
                ShouldRetouch: false,
                Reason: ToolbarInteractionTopmostRetouchReason.SceneNotInteractive);
        }

        return new ToolbarInteractionTopmostRetouchDecision(
            ShouldRetouch: true,
            Reason: ToolbarInteractionTopmostRetouchReason.InteractiveSceneActive);
    }

    internal static bool ShouldRetouch(
        bool overlayVisible,
        bool photoModeActive,
        bool whiteboardActive)
    {
        return Resolve(
            overlayVisible,
            photoModeActive,
            whiteboardActive).ShouldRetouch;
    }
}
