namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionDuplicateReasonPolicy
{
    internal static string ResolveTag(SessionTransitionDuplicateReason reason)
    {
        return reason switch
        {
            SessionTransitionDuplicateReason.TransitionAdvanced => "transition-advanced",
            SessionTransitionDuplicateReason.DuplicateTransitionId => "duplicate-transition-id",
            SessionTransitionDuplicateReason.RegressedTransitionId => "regressed-transition-id",
            _ => "none"
        };
    }
}
