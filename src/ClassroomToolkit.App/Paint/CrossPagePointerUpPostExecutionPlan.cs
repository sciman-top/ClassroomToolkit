namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPagePointerUpPostExecutionPlan(
    bool ShouldTrackPointerUp,
    bool ShouldApplyFastRefresh,
    bool ShouldScheduleDeferredRefresh,
    string DeferredRefreshSource,
    bool ShouldFlushReplay,
    bool ShouldEndFirstInputTrace,
    bool ShouldRequestInkContextRefresh);
