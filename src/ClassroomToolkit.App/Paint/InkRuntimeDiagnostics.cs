namespace ClassroomToolkit.App.Paint;

internal sealed class InkRuntimeDiagnostics
{
    internal static InkRuntimeDiagnostics? CreateFromEnvironment() => null;

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
}
