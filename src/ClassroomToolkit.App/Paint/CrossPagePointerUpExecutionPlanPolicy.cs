namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpExecutionPlan(
    bool ShouldTrackPointerUp,
    bool ShouldApplyFastRefresh,
    bool ShouldScheduleDeferredRefresh,
    string DeferredRefreshSource,
    bool ShouldFlushReplay,
    bool ShouldRequestInkContextRefresh);

internal static class CrossPagePointerUpExecutionPlanPolicy
{
    internal static CrossPagePointerUpExecutionPlan Resolve(
        CrossPagePointerUpDecision decision,
        bool hadInkOperation,
        bool pendingInkContextCheck)
    {
        return new CrossPagePointerUpExecutionPlan(
            ShouldTrackPointerUp: decision.ShouldTrackPointerUp,
            ShouldApplyFastRefresh: decision.ShouldSchedulePostInputRefresh,
            ShouldScheduleDeferredRefresh: decision.ShouldSchedulePostInputRefresh,
            DeferredRefreshSource: CrossPagePointerUpRefreshSourcePolicy.Resolve(hadInkOperation),
            ShouldFlushReplay: decision.ShouldFlushReplay,
            ShouldRequestInkContextRefresh: pendingInkContextCheck);
    }
}
