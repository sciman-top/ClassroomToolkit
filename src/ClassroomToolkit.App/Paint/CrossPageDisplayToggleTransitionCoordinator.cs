using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct CrossPageDisplayToggleTransitionExecutionResult(
    bool AppliedFlagUpdate,
    bool ResetNormalizedWidth,
    bool RestoredUnifiedTransformAndRedraw,
    bool SavedUnifiedTransformState,
    bool ResetReplayAndClearedNeighbors,
    bool RefreshedImageSequenceSource,
    bool ReloadedPdfInkCache);

internal static class CrossPageDisplayToggleTransitionCoordinator
{
    internal static CrossPageDisplayToggleTransitionExecutionResult Apply(
        bool currentCrossPageDisplayEnabled,
        bool requestedEnabled,
        bool photoInkModeActive,
        bool photoDocumentIsPdf,
        bool photoUnifiedTransformReady,
        Action<bool> setCrossPageDisplayEnabled,
        Action resetCrossPageNormalizedWidth,
        Action restoreUnifiedTransformAndRedraw,
        Action saveUnifiedTransformState,
        Action updateCurrentPageWidthNormalization,
        Action resetCrossPageReplayState,
        Action clearNeighborPages,
        Action refreshCurrentImageSequenceSourceAfterToggle,
        Action reloadPdfInkCacheAfterToggle)
    {
        ArgumentNullException.ThrowIfNull(setCrossPageDisplayEnabled);
        ArgumentNullException.ThrowIfNull(resetCrossPageNormalizedWidth);
        ArgumentNullException.ThrowIfNull(restoreUnifiedTransformAndRedraw);
        ArgumentNullException.ThrowIfNull(saveUnifiedTransformState);
        ArgumentNullException.ThrowIfNull(updateCurrentPageWidthNormalization);
        ArgumentNullException.ThrowIfNull(resetCrossPageReplayState);
        ArgumentNullException.ThrowIfNull(clearNeighborPages);
        ArgumentNullException.ThrowIfNull(refreshCurrentImageSequenceSourceAfterToggle);
        ArgumentNullException.ThrowIfNull(reloadPdfInkCacheAfterToggle);

        var flagUpdate = CrossPageDisplayToggleFlagUpdatePolicy.Resolve(
            currentCrossPageDisplayEnabled,
            requestedEnabled);
        if (!flagUpdate.ShouldApply)
        {
            return default;
        }

        PaintActionInvoker.TryInvoke(() => setCrossPageDisplayEnabled(flagUpdate.NextCrossPageDisplayEnabled));

        var togglePlan = CrossPageDisplayToggleRuntimePlanPolicy.Resolve(
            photoInkModeActive: photoInkModeActive,
            crossPageDisplayEnabled: flagUpdate.NextCrossPageDisplayEnabled,
            photoDocumentIsPdf: photoDocumentIsPdf,
            photoUnifiedTransformReady: photoUnifiedTransformReady);

        PaintActionInvoker.TryInvoke(resetCrossPageNormalizedWidth);

        if (togglePlan.ShouldRestoreUnifiedTransformAndRedraw)
        {
            PaintActionInvoker.TryInvoke(restoreUnifiedTransformAndRedraw);
        }

        if (togglePlan.ShouldSaveUnifiedTransformState)
        {
            PaintActionInvoker.TryInvoke(saveUnifiedTransformState);
            PaintActionInvoker.TryInvoke(updateCurrentPageWidthNormalization);
        }

        if (togglePlan.ShouldResetReplayAndClearNeighbors)
        {
            PaintActionInvoker.TryInvoke(resetCrossPageReplayState);
            PaintActionInvoker.TryInvoke(clearNeighborPages);
            PaintActionInvoker.TryInvoke(updateCurrentPageWidthNormalization);
        }

        if (togglePlan.ShouldRefreshImageSequenceSource)
        {
            PaintActionInvoker.TryInvoke(refreshCurrentImageSequenceSourceAfterToggle);
        }

        if (togglePlan.ShouldReloadPdfInkCache)
        {
            PaintActionInvoker.TryInvoke(reloadPdfInkCacheAfterToggle);
        }

        return new CrossPageDisplayToggleTransitionExecutionResult(
            AppliedFlagUpdate: true,
            ResetNormalizedWidth: true,
            RestoredUnifiedTransformAndRedraw: togglePlan.ShouldRestoreUnifiedTransformAndRedraw,
            SavedUnifiedTransformState: togglePlan.ShouldSaveUnifiedTransformState,
            ResetReplayAndClearedNeighbors: togglePlan.ShouldResetReplayAndClearNeighbors,
            RefreshedImageSequenceSource: togglePlan.ShouldRefreshImageSequenceSource,
            ReloadedPdfInkCache: togglePlan.ShouldReloadPdfInkCache);
    }

}
