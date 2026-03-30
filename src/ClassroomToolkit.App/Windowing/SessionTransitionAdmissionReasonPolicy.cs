namespace ClassroomToolkit.App.Windowing;

internal static class SessionTransitionAdmissionReasonPolicy
{
    internal static string ResolveTag(SessionTransitionAdmissionReason reason)
    {
        return reason switch
        {
            SessionTransitionAdmissionReason.NoStateChange => "no-state-change",
            SessionTransitionAdmissionReason.DuplicateTransitionId => "duplicate-transition-id",
            SessionTransitionAdmissionReason.RegressedTransitionId => "regressed-transition-id",
            _ => "accepted"
        };
    }
}
