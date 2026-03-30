namespace ClassroomToolkit.App;

internal readonly record struct InkStartupCleanupSummary(
    int TotalSidecars,
    int TotalComposites);

internal static class InkStartupCleanupLogPolicy
{
    internal static bool ShouldLogDeletionSummary(InkStartupCleanupSummary summary)
    {
        return summary.TotalSidecars > 0 || summary.TotalComposites > 0;
    }

    internal static string FormatDeletionSummary(InkStartupCleanupSummary summary)
    {
        return $"[InkStartupCleanup] deleted orphan sidecars={summary.TotalSidecars}, composites={summary.TotalComposites}";
    }

    internal static string FormatFailureMessage(string message)
    {
        return $"[InkStartupCleanup] failed: {message}";
    }
}
