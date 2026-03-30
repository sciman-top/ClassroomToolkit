namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayToggleRuntimePlan(
    bool ShouldRestoreUnifiedTransformAndRedraw,
    bool ShouldSaveUnifiedTransformState,
    bool ShouldResetReplayAndClearNeighbors,
    bool ShouldRefreshImageSequenceSource,
    bool ShouldReloadPdfInkCache);

internal static class CrossPageDisplayToggleRuntimePlanPolicy
{
    internal static CrossPageDisplayToggleRuntimePlan Resolve(
        bool photoInkModeActive,
        bool crossPageDisplayEnabled,
        bool photoDocumentIsPdf,
        bool photoUnifiedTransformReady)
    {
        return new CrossPageDisplayToggleRuntimePlan(
            ShouldRestoreUnifiedTransformAndRedraw: photoInkModeActive && crossPageDisplayEnabled && photoUnifiedTransformReady,
            ShouldSaveUnifiedTransformState: photoInkModeActive && crossPageDisplayEnabled && !photoUnifiedTransformReady,
            ShouldResetReplayAndClearNeighbors: !crossPageDisplayEnabled,
            ShouldRefreshImageSequenceSource: photoInkModeActive && !photoDocumentIsPdf,
            ShouldReloadPdfInkCache: photoInkModeActive && photoDocumentIsPdf);
    }
}
