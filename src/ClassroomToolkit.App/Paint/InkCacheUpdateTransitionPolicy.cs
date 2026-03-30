namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkCacheUpdateTransitionPlan(
    bool ShouldStartMonitor,
    bool ShouldClearCache,
    bool ShouldRequestRefresh);

internal static class InkCacheUpdateTransitionPolicy
{
    internal static InkCacheUpdateTransitionPlan Resolve(bool enabled, bool monitorEnabled)
    {
        return new InkCacheUpdateTransitionPlan(
            ShouldStartMonitor: !monitorEnabled,
            ShouldClearCache: !enabled,
            ShouldRequestRefresh: true);
    }
}
