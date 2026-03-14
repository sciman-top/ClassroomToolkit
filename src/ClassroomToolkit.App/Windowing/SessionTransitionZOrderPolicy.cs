using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal enum SessionTransitionZOrderRetouchReason
{
    None = 0,
    SceneChangedToSurface = 1,
    SceneUnchanged = 2,
    SceneChangedToNoneSurface = 3
}

internal readonly record struct SessionTransitionZOrderRetouchDecision(
    bool ShouldRetouchSurface,
    SessionTransitionZOrderRetouchReason Reason);

internal static class SessionTransitionZOrderPolicy
{
    internal static SessionTransitionZOrderRetouchDecision Resolve(UiSessionState previous, UiSessionState current)
    {
        if (previous.Scene == current.Scene)
        {
            return new SessionTransitionZOrderRetouchDecision(
                ShouldRetouchSurface: false,
                Reason: SessionTransitionZOrderRetouchReason.SceneUnchanged);
        }

        return UiSceneSurfaceMapper.Map(current.Scene) != ZOrderSurface.None
            ? new SessionTransitionZOrderRetouchDecision(
                ShouldRetouchSurface: true,
                Reason: SessionTransitionZOrderRetouchReason.SceneChangedToSurface)
            : new SessionTransitionZOrderRetouchDecision(
                ShouldRetouchSurface: false,
                Reason: SessionTransitionZOrderRetouchReason.SceneChangedToNoneSurface);
    }

    internal static bool ShouldRetouchSurface(UiSessionState previous, UiSessionState current)
    {
        return Resolve(previous, current).ShouldRetouchSurface;
    }
}
