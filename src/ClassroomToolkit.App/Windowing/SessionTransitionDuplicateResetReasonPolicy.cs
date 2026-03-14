namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionDuplicateResetReasonPolicy
{
    internal static string ResolveTag(SessionTransitionDuplicateResetReason reason)
    {
        return reason switch
        {
            SessionTransitionDuplicateResetReason.OverlayNotRewired => "overlay-not-rewired",
            SessionTransitionDuplicateResetReason.NoAppliedTransition => "no-applied-transition",
            SessionTransitionDuplicateResetReason.ResetRequired => "reset-required",
            _ => "none"
        };
    }
}
