namespace ClassroomToolkit.App.Windowing;

internal static class SurfaceZOrderDecisionDiagnosticsPolicy
{
    internal static string FormatDedupSkipMessage(SurfaceZOrderDecisionDedupReason reason)
    {
        return $"[SurfaceZOrder][Dedup] skip reason={SurfaceZOrderDecisionDedupReasonPolicy.ResolveTag(reason)}";
    }
}
