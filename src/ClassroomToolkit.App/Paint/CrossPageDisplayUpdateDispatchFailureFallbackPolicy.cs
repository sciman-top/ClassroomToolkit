namespace ClassroomToolkit.App.Paint;

internal enum CrossPageDisplayUpdateDispatchFailureFallbackReason
{
    None = 0,
    InlineCurrentThread = 1,
    QueueReplay = 2
}

internal readonly record struct CrossPageDisplayUpdateDispatchFailureFallbackDecision(
    bool ShouldRunInline,
    bool ShouldQueueReplay,
    CrossPageDisplayUpdateDispatchFailureFallbackReason Reason);

internal static class CrossPageDisplayUpdateDispatchFailureFallbackPolicy
{
    internal static CrossPageDisplayUpdateDispatchFailureFallbackDecision Resolve(
        bool dispatchScheduled,
        bool dispatcherCheckAccess,
        bool dispatcherShutdownStarted,
        bool dispatcherShutdownFinished)
    {
        if (dispatchScheduled)
        {
            return new CrossPageDisplayUpdateDispatchFailureFallbackDecision(
                ShouldRunInline: false,
                ShouldQueueReplay: false,
                Reason: CrossPageDisplayUpdateDispatchFailureFallbackReason.None);
        }

        if (dispatcherCheckAccess && !dispatcherShutdownStarted && !dispatcherShutdownFinished)
        {
            return new CrossPageDisplayUpdateDispatchFailureFallbackDecision(
                ShouldRunInline: true,
                ShouldQueueReplay: false,
                Reason: CrossPageDisplayUpdateDispatchFailureFallbackReason.InlineCurrentThread);
        }

        return new CrossPageDisplayUpdateDispatchFailureFallbackDecision(
            ShouldRunInline: false,
            ShouldQueueReplay: true,
            Reason: CrossPageDisplayUpdateDispatchFailureFallbackReason.QueueReplay);
    }
}
