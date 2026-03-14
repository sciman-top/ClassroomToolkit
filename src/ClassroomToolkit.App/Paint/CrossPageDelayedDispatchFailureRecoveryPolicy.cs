namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDelayedDispatchFailureRecoveryDecision(
    bool ShouldRecoverInline);

internal static class CrossPageDelayedDispatchFailureRecoveryPolicy
{
    internal static CrossPageDelayedDispatchFailureRecoveryDecision Resolve(
        bool recoveryDispatchScheduled,
        bool dispatcherCheckAccess,
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        if (recoveryDispatchScheduled)
        {
            return new CrossPageDelayedDispatchFailureRecoveryDecision(
                ShouldRecoverInline: false);
        }

        if (dispatcherShutdownStarted || dispatcherShutdownFinished)
        {
            return new CrossPageDelayedDispatchFailureRecoveryDecision(
                ShouldRecoverInline: false);
        }

        return new CrossPageDelayedDispatchFailureRecoveryDecision(
            ShouldRecoverInline: dispatcherCheckAccess);
    }
}
