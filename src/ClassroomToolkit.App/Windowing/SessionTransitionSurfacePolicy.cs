using ClassroomToolkit.App.Session;

namespace ClassroomToolkit.App.Windowing;

internal readonly record struct SessionTransitionSurfaceDecision(
    bool ShouldTouchSurface,
    ZOrderSurface Surface,
    SessionTransitionSurfaceReason Reason);

internal enum SessionTransitionSurfaceReason
{
    None = 0,
    SurfaceRetouchRequested = 1,
    NoSurfaceRetouchRequested = 2
}

internal static class SessionTransitionSurfacePolicy
{
    internal static SessionTransitionSurfaceDecision Resolve(UiSessionState previous, UiSessionState current)
    {
        var shouldTouch = SessionTransitionZOrderPolicy.ShouldRetouchSurface(previous, current);
        var surface = shouldTouch
            ? UiSceneSurfaceMapper.Map(current.Scene)
            : ZOrderSurface.None;
        var reason = shouldTouch
            ? SessionTransitionSurfaceReason.SurfaceRetouchRequested
            : SessionTransitionSurfaceReason.NoSurfaceRetouchRequested;
        return new SessionTransitionSurfaceDecision(shouldTouch, surface, reason);
    }
}
