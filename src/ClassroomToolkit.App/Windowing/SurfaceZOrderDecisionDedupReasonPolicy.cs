namespace ClassroomToolkit.App.Windowing;

internal static class SurfaceZOrderDecisionDedupReasonPolicy
{
    internal static string ResolveTag(SurfaceZOrderDecisionDedupReason reason)
    {
        return reason switch
        {
            SurfaceZOrderDecisionDedupReason.NoHistory => "no-history",
            SurfaceZOrderDecisionDedupReason.DedupDisabledByInterval => "interval-disabled",
            SurfaceZOrderDecisionDedupReason.UnsetTimestamp => "unset-timestamp",
            SurfaceZOrderDecisionDedupReason.SkippedWithinDedupWindow => "skipped-within-window",
            SurfaceZOrderDecisionDedupReason.Applied => "applied",
            _ => "none"
        };
    }
}
