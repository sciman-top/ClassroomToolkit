namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionSurfaceReasonPolicy
{
    internal static string ResolveTag(SessionTransitionSurfaceReason reason)
    {
        return reason switch
        {
            SessionTransitionSurfaceReason.SurfaceRetouchRequested => "surface-retouch-requested",
            SessionTransitionSurfaceReason.NoSurfaceRetouchRequested => "no-surface-retouch-requested",
            _ => "none"
        };
    }
}
