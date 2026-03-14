using System;

namespace ClassroomToolkit.App.Paint;

internal readonly record struct InkShowTransitionExecutionResult(
    bool AppliedSetting,
    bool ReturnedAfterSetting,
    bool ClearedInkState,
    bool LoadedCurrentPage,
    bool RequestedCrossPageUpdate);

internal static class InkShowTransitionCoordinator
{
    internal static InkShowTransitionExecutionResult Apply(
        bool currentInkShowEnabled,
        bool requestedEnabled,
        bool photoModeActive,
        Action<bool> setInkShowEnabled,
        Action purgePersistedInkForHiddenCurrentDocument,
        Action clearInkSurfaceState,
        Action clearNeighborInkVisuals,
        Action clearNeighborInkCache,
        Action clearNeighborInkRenderPending,
        Action clearNeighborInkSidecarLoadPending,
        Action loadCurrentPageIfExists,
        Action<string> requestCrossPageDisplayUpdate)
    {
        ArgumentNullException.ThrowIfNull(setInkShowEnabled);
        ArgumentNullException.ThrowIfNull(purgePersistedInkForHiddenCurrentDocument);
        ArgumentNullException.ThrowIfNull(clearInkSurfaceState);
        ArgumentNullException.ThrowIfNull(clearNeighborInkVisuals);
        ArgumentNullException.ThrowIfNull(clearNeighborInkCache);
        ArgumentNullException.ThrowIfNull(clearNeighborInkRenderPending);
        ArgumentNullException.ThrowIfNull(clearNeighborInkSidecarLoadPending);
        ArgumentNullException.ThrowIfNull(loadCurrentPageIfExists);
        ArgumentNullException.ThrowIfNull(requestCrossPageDisplayUpdate);

        var transitionPlan = InkShowUpdateTransitionPolicy.Resolve(
            currentInkShowEnabled,
            requestedEnabled,
            photoModeActive);
        if (!transitionPlan.ShouldApplySetting)
        {
            return default;
        }

        setInkShowEnabled(requestedEnabled);
        if (transitionPlan.ShouldReturnAfterSetting)
        {
            return new InkShowTransitionExecutionResult(
                AppliedSetting: true,
                ReturnedAfterSetting: true,
                ClearedInkState: false,
                LoadedCurrentPage: false,
                RequestedCrossPageUpdate: false);
        }

        if (transitionPlan.ShouldClearInkState)
        {
            purgePersistedInkForHiddenCurrentDocument();
            clearInkSurfaceState();
            clearNeighborInkVisuals();
            clearNeighborInkCache();
            clearNeighborInkRenderPending();
            clearNeighborInkSidecarLoadPending();
            if (transitionPlan.RequestCrossPageUpdateForDisabled)
            {
                requestCrossPageDisplayUpdate(CrossPageUpdateSources.InkShowDisabled);
            }

            return new InkShowTransitionExecutionResult(
                AppliedSetting: true,
                ReturnedAfterSetting: false,
                ClearedInkState: true,
                LoadedCurrentPage: false,
                RequestedCrossPageUpdate: transitionPlan.RequestCrossPageUpdateForDisabled);
        }

        if (transitionPlan.ShouldLoadCurrentPage)
        {
            loadCurrentPageIfExists();
        }

        if (transitionPlan.RequestCrossPageUpdateForEnabled)
        {
            requestCrossPageDisplayUpdate(CrossPageUpdateSources.InkShowEnabled);
        }

        return new InkShowTransitionExecutionResult(
            AppliedSetting: true,
            ReturnedAfterSetting: false,
            ClearedInkState: false,
            LoadedCurrentPage: transitionPlan.ShouldLoadCurrentPage,
            RequestedCrossPageUpdate: transitionPlan.RequestCrossPageUpdateForEnabled);
    }
}
