namespace ClassroomToolkit.App.Paint;

internal enum CrossPageReplayDispatchScheduleFallbackReason
{
    None = 0,
    InlineCurrentThread = 1,
    RequeuePending = 2
}

internal readonly record struct CrossPageReplayDispatchScheduleFallbackDecision(
    bool ShouldRunInline,
    bool ShouldRequeuePending,
    CrossPageReplayDispatchScheduleFallbackReason Reason);

internal static class CrossPageReplayDispatchScheduleFallbackPolicy
{
    internal static CrossPageReplayDispatchScheduleFallbackDecision Resolve(
        bool dispatchScheduled,
        bool dispatcherCheckAccess,
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        if (dispatchScheduled)
        {
            return new CrossPageReplayDispatchScheduleFallbackDecision(
                ShouldRunInline: false,
                ShouldRequeuePending: false,
                Reason: CrossPageReplayDispatchScheduleFallbackReason.None);
        }

        if (dispatcherCheckAccess && !dispatcherShutdownStarted && !dispatcherShutdownFinished)
        {
            return new CrossPageReplayDispatchScheduleFallbackDecision(
                ShouldRunInline: true,
                ShouldRequeuePending: false,
                Reason: CrossPageReplayDispatchScheduleFallbackReason.InlineCurrentThread);
        }

        return new CrossPageReplayDispatchScheduleFallbackDecision(
            ShouldRunInline: false,
            ShouldRequeuePending: true,
            Reason: CrossPageReplayDispatchScheduleFallbackReason.RequeuePending);
    }
}
