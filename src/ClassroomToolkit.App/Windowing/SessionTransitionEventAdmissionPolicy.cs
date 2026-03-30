namespace ClassroomToolkit.App.Windowing;

internal enum SessionTransitionAdmissionReason
{
    None = 0,
    NoStateChange = 1,
    DuplicateTransitionId = 2,
    RegressedTransitionId = 3
}

internal readonly record struct SessionTransitionAdmissionDecision(
    bool ShouldProcess,
    SessionTransitionAdmissionReason Reason);

internal static class SessionTransitionEventAdmissionPolicy
{
    internal static SessionTransitionAdmissionDecision Resolve(
        bool hasStateChange,
        long lastAppliedTransitionId,
        long currentTransitionId)
    {
        if (!hasStateChange)
        {
            return new SessionTransitionAdmissionDecision(
                ShouldProcess: false,
                Reason: SessionTransitionAdmissionReason.NoStateChange);
        }

        var duplicateDecision = SessionTransitionDuplicatePolicy.Resolve(
            lastAppliedTransitionId,
            currentTransitionId);
        if (!duplicateDecision.ShouldApply)
        {
            return new SessionTransitionAdmissionDecision(
                ShouldProcess: false,
                Reason: duplicateDecision.Reason == SessionTransitionDuplicateReason.RegressedTransitionId
                    ? SessionTransitionAdmissionReason.RegressedTransitionId
                    : SessionTransitionAdmissionReason.DuplicateTransitionId);
        }

        return new SessionTransitionAdmissionDecision(
            ShouldProcess: true,
            Reason: SessionTransitionAdmissionReason.None);
    }

    internal static bool ShouldProcess(
        bool hasStateChange,
        long lastAppliedTransitionId,
        long currentTransitionId)
    {
        return Resolve(
            hasStateChange,
            lastAppliedTransitionId,
            currentTransitionId).ShouldProcess;
    }
}
