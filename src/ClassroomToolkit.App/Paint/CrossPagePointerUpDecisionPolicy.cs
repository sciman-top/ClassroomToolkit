namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpDecision(
    bool ShouldTrackPointerUp,
    bool ShouldSchedulePostInputRefresh,
    bool ShouldFlushReplay,
    bool ShouldRequestImmediateRefresh);

internal static class CrossPagePointerUpDecisionPolicy
{
    internal static CrossPagePointerUpDecision Resolve(
        bool crossPageDisplayActive,
        bool hadInkOperation,
        bool deferredRefreshRequested,
        bool updatePending)
    {
        var shouldSchedule = CrossPagePointerUpRefreshPolicy.ShouldSchedulePostInputRefresh(
            crossPageDisplayActive,
            hadInkOperation,
            deferredRefreshRequested);
        var shouldRequestImmediateRefresh = CrossPagePointerUpImmediateRefreshPolicy.ShouldRequest(
            crossPageDisplayActive,
            hadInkOperation,
            deferredRefreshRequested,
            updatePending);

        return new CrossPagePointerUpDecision(
            ShouldTrackPointerUp: crossPageDisplayActive,
            ShouldSchedulePostInputRefresh: shouldSchedule,
            ShouldFlushReplay: crossPageDisplayActive,
            ShouldRequestImmediateRefresh: shouldRequestImmediateRefresh);
    }
}
