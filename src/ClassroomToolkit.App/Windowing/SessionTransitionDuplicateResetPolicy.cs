namespace ClassroomToolkit.App.Windowing;

internal enum SessionTransitionDuplicateResetReason
{
    None = 0,
    OverlayNotRewired = 1,
    NoAppliedTransition = 2,
    ResetRequired = 3
}

internal readonly record struct SessionTransitionDuplicateResetDecision(
    bool ShouldReset,
    SessionTransitionDuplicateResetReason Reason);

internal static class SessionTransitionDuplicateResetPolicy
{
    internal static SessionTransitionDuplicateResetDecision Resolve(
        bool overlayWindowRewired,
        long lastAppliedTransitionId)
    {
        if (!overlayWindowRewired)
        {
            return new SessionTransitionDuplicateResetDecision(
                ShouldReset: false,
                Reason: SessionTransitionDuplicateResetReason.OverlayNotRewired);
        }

        if (lastAppliedTransitionId <= 0)
        {
            return new SessionTransitionDuplicateResetDecision(
                ShouldReset: false,
                Reason: SessionTransitionDuplicateResetReason.NoAppliedTransition);
        }

        return new SessionTransitionDuplicateResetDecision(
            ShouldReset: true,
            Reason: SessionTransitionDuplicateResetReason.ResetRequired);
    }

    internal static bool ShouldReset(
        bool overlayWindowRewired,
        long lastAppliedTransitionId)
    {
        return Resolve(overlayWindowRewired, lastAppliedTransitionId).ShouldReset;
    }
}
