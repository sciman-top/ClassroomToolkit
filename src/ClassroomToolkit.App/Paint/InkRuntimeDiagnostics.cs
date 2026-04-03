namespace ClassroomToolkit.App.Paint;

internal sealed class InkRuntimeDiagnostics
{
    private readonly bool _inkRedrawTelemetryEnabled;

    private InkRuntimeDiagnostics(bool inkRedrawTelemetryEnabled)
    {
        _inkRedrawTelemetryEnabled = inkRedrawTelemetryEnabled;
    }

    internal static InkRuntimeDiagnostics? CreateFromEnvironment()
    {
        var inkRedrawTelemetryEnabled = InkRedrawTelemetryPolicy.ResolveEnabledFromEnvironment();
        if (!inkRedrawTelemetryEnabled)
        {
            return null;
        }

        return new InkRuntimeDiagnostics(inkRedrawTelemetryEnabled);
    }

    internal void OnInkInput()
    {
    }

    internal void OnRedrawRequested(bool throttled)
    {
    }

    internal void OnRedrawCompleted(double elapsedMs)
    {
    }

    internal void OnAutoSaveDeferred(string reason)
    {
    }

    internal void OnAutoSavePersistResult(bool persisted)
    {
    }

    internal void OnAutoSaveFailure()
    {
    }

    internal void OnSyncPersist()
    {
    }

    internal void OnCrossPageFirstInputEvent(long traceId, string stage, double elapsedMs, string? details = null)
    {
    }

    internal void OnCrossPageUpdateEvent(string stage, string source, string? details = null)
    {
    }

    internal void OnInkRedrawTelemetry(
        int sampleCount,
        double partialHitRate,
        int windowCount,
        int windowSize,
        double allP50Ms,
        double allP95Ms,
        double partialP95Ms,
        double fullP95Ms)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }

        System.Diagnostics.Debug.WriteLine(
            $"[InkRedrawTelemetry] samples={sampleCount} partial-hit={partialHitRate:F1}% " +
            $"window={windowCount}/{windowSize} " +
            $"all(p50/p95)={allP50Ms:F2}/{allP95Ms:F2}ms " +
            $"partial(p95)={partialP95Ms:F2}ms full(p95)={fullP95Ms:F2}ms");
    }
}
