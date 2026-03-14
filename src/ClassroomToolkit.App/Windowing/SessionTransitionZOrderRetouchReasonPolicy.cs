namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionZOrderRetouchReasonPolicy
{
    internal static string ResolveTag(SessionTransitionZOrderRetouchReason reason)
    {
        return reason switch
        {
            SessionTransitionZOrderRetouchReason.SceneChangedToSurface => "scene-changed-to-surface",
            SessionTransitionZOrderRetouchReason.SceneUnchanged => "scene-unchanged",
            SessionTransitionZOrderRetouchReason.SceneChangedToNoneSurface => "scene-changed-to-none-surface",
            _ => "none"
        };
    }
}
