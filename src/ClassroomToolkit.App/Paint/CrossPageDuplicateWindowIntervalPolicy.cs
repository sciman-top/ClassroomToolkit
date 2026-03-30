namespace ClassroomToolkit.App.Paint;

internal static class CrossPageDuplicateWindowIntervalPolicy
{
    internal static int Resolve(
        int configuredWindowMs,
        int minimumWindowMs)
    {
        var normalizedConfigured = Math.Max(CrossPageDuplicateWindowThresholds.MinWindowMs, configuredWindowMs);
        return Math.Max(
            normalizedConfigured,
            Math.Max(CrossPageDuplicateWindowThresholds.MinWindowMs, minimumWindowMs));
    }
}
