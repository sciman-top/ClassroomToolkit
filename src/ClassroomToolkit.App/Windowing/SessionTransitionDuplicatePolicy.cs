namespace ClassroomToolkit.App.Windowing;

internal enum SessionTransitionDuplicateReason
{
    None = 0,
    TransitionAdvanced = 1,
    DuplicateTransitionId = 2,
    RegressedTransitionId = 3
}

internal readonly record struct SessionTransitionDuplicateDecision(
    bool ShouldApply,
    SessionTransitionDuplicateReason Reason);

internal static class SessionTransitionDuplicatePolicy
{
    internal static SessionTransitionDuplicateDecision Resolve(long lastAppliedTransitionId, long currentTransitionId)
    {
        if (currentTransitionId > lastAppliedTransitionId)
        {
            return new SessionTransitionDuplicateDecision(
                ShouldApply: true,
                Reason: SessionTransitionDuplicateReason.TransitionAdvanced);
        }

        if (currentTransitionId == lastAppliedTransitionId)
        {
            return new SessionTransitionDuplicateDecision(
                ShouldApply: false,
                Reason: SessionTransitionDuplicateReason.DuplicateTransitionId);
        }

        return new SessionTransitionDuplicateDecision(
            ShouldApply: false,
            Reason: SessionTransitionDuplicateReason.RegressedTransitionId);
    }

    internal static bool ShouldApply(long lastAppliedTransitionId, long currentTransitionId)
    {
        return Resolve(lastAppliedTransitionId, currentTransitionId).ShouldApply;
    }
}
