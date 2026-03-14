namespace ClassroomToolkit.App.Paint;

internal static class CrossPagePointerUpPostExecutionPolicy
{
    internal static CrossPagePointerUpPostExecutionPlan Resolve(
        CrossPagePointerUpExecutionPlan executionPlan,
        bool crossPageFirstInputTraceActive)
    {
        return new CrossPagePointerUpPostExecutionPlan(
            ShouldTrackPointerUp: executionPlan.ShouldTrackPointerUp,
            ShouldApplyFastRefresh: executionPlan.ShouldApplyFastRefresh,
            ShouldScheduleDeferredRefresh: executionPlan.ShouldScheduleDeferredRefresh,
            DeferredRefreshSource: executionPlan.DeferredRefreshSource,
            ShouldFlushReplay: executionPlan.ShouldFlushReplay,
            ShouldEndFirstInputTrace: crossPageFirstInputTraceActive,
            ShouldRequestInkContextRefresh: executionPlan.ShouldRequestInkContextRefresh);
    }
}
