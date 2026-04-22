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
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnRedrawRequested(bool throttled)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnRedrawCompleted(double elapsedMs)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnAutoSaveDeferred(string reason)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnAutoSavePersistResult(bool persisted)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnAutoSaveFailure()
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnSyncPersist()
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnCrossPageFirstInputEvent(long traceId, string stage, double elapsedMs, string? details = null)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
    }

    internal void OnCrossPageUpdateEvent(string stage, string source, string? details = null)
    {
        if (!_inkRedrawTelemetryEnabled)
        {
            return;
        }
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
