using System;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Utilities;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void UpdateInkCacheEnabled(bool enabled)
    {
        var transitionPlan = InkCacheUpdateTransitionPolicy.Resolve(
            enabled,
            _inkMonitor.IsEnabled);
        _inkCacheEnabled = enabled;
        if (transitionPlan.ShouldStartMonitor)
        {
            _inkMonitor.Start();
        }
        if (transitionPlan.ShouldClearCache)
        {
            _photoCache.Clear();
        }
        if (transitionPlan.ShouldRequestRefresh)
        {
            _refreshOrchestrator.RequestRefresh("ink-cache");
        }
    }

    public void UpdateInkSaveEnabled(bool enabled)
    {
        var transitionPlan = InkSaveUpdateTransitionPolicy.Resolve(enabled);
        _inkSaveEnabled = enabled;
        if (transitionPlan.ShouldStopAutoSaveTimer)
        {
            _inkSidecarAutoSaveTimer?.Stop();
            if (transitionPlan.ShouldCancelPendingAutoSave)
            {
                _inkSidecarAutoSaveGate.NextGeneration();
            }
            return;
        }
        if (transitionPlan.ShouldScheduleAutoSave)
        {
            ScheduleSidecarAutoSave();
        }
    }

    public void UpdateInkShowEnabled(bool enabled)
    {
        InkShowTransitionCoordinator.Apply(
            currentInkShowEnabled: _inkShowEnabled,
            requestedEnabled: enabled,
            photoModeActive: _photoModeActive,
            setInkShowEnabled: nextEnabled => _inkShowEnabled = nextEnabled,
            purgePersistedInkForHiddenCurrentDocument: PurgePersistedInkForHiddenCurrentDocumentIfNeeded,
            clearInkSurfaceState: ClearInkSurfaceState,
            clearNeighborInkVisuals: () => ClearNeighborInkVisuals(clearSlotIdentity: true),
            clearNeighborInkCache: () => _neighborInkCache.Clear(),
            clearNeighborInkRenderPending: () => _neighborInkRenderPending.Clear(),
            clearNeighborInkSidecarLoadPending: () => _neighborInkSidecarLoadPending.Clear(),
            loadCurrentPageIfExists: () => LoadCurrentPageIfExists(),
            requestCrossPageDisplayUpdate: RequestCrossPageDisplayUpdate);
    }

    public void UpdateInkRecordEnabled(bool enabled)
    {
        _inkRecordEnabled = enabled;
    }

    public void UpdateInkReplayPreviousEnabled(bool enabled)
    {
        _inkReplayPreviousEnabled = enabled;
    }

    public void UpdateInkRetentionDays(int days)
    {
        _inkRetentionDays = Math.Max(0, days);
        if (_inkRetentionDays > 0 && _inkRecordEnabled)
        {
            var lifecycleToken = _overlayLifecycleCancellation.Token;
            _ = SafeTaskRunner.Run(
                "PaintOverlayWindow.UpdateInkRetentionDays",
                _ => _inkStorage.CleanupOldRecords(_inkRetentionDays),
                lifecycleToken,
                onError: ex => System.Diagnostics.Debug.WriteLine(
                    $"[InkStorage] retention-cleanup failed: {ex.GetType().Name} - {ex.Message}"));
        }
    }

    public void UpdateInkPhotoRootPath(string path)
    {
        _inkPhotoRootPath = path ?? string.Empty;
        _inkStorage = new InkStorageService(photoRootPath: _inkPhotoRootPath);
    }

    public void UpdatePhotoTransformMemoryEnabled(bool enabled)
    {
        _rememberPhotoTransform = enabled;
        if (PhotoTransformMemoryTogglePolicy.ShouldResetUserDirtyState(_rememberPhotoTransform))
        {
            _photoUserTransformDirty = false;
            _photoPageTransforms.Clear();
        }
        if (PhotoTransformMemoryTogglePolicy.ShouldResetUnifiedTransformState(_rememberPhotoTransform))
        {
            _photoUnifiedTransformReady = false;
        }
    }

    public void LoadInkPage(int pageIndex)
    {
        // Ink history view is removed; keep for compatibility.
    }

    public void UpdateCrossPageDisplayEnabled(bool enabled)
    {
        CrossPageDisplayToggleTransitionCoordinator.Apply(
            currentCrossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled(),
            requestedEnabled: enabled,
            photoInkModeActive: IsPhotoInkModeActive(),
            photoDocumentIsPdf: _photoDocumentIsPdf,
            photoUnifiedTransformReady: _photoUnifiedTransformReady,
            setCrossPageDisplayEnabled: nextEnabled => _crossPageDisplayEnabled = nextEnabled,
            resetCrossPageNormalizedWidth: ResetCrossPageNormalizedWidth,
            restoreUnifiedTransformAndRedraw: RestoreUnifiedPhotoTransformAndRequestRedraw,
            saveUnifiedTransformState: () => SavePhotoTransformState(userAdjusted: _photoUserTransformDirty),
            updateCurrentPageWidthNormalization: () => UpdateCurrentPageWidthNormalization(),
            resetCrossPageReplayState: ResetCrossPageReplayState,
            clearNeighborPages: ClearNeighborPages,
            refreshCurrentImageSequenceSourceAfterToggle: RefreshCurrentImageSequenceSourceAfterCrossPageToggle,
            reloadPdfInkCacheAfterToggle: ReloadPdfInkCacheAfterCrossPageToggle);
    }

    private void RestoreUnifiedPhotoTransformAndRequestRedraw()
    {
        ApplyLastUnifiedPhotoTransform(markUserDirty: true);
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        RequestInkRedraw();
        UpdateCurrentPageWidthNormalization();
    }

    private void ApplyLastUnifiedPhotoTransform(bool markUserDirty)
    {
        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = _lastPhotoScaleX;
        _photoScale.ScaleY = _lastPhotoScaleY;
        _photoTranslate.X = _lastPhotoTranslateX;
        _photoTranslate.Y = _lastPhotoTranslateY;
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        if (markUserDirty)
        {
            _photoUserTransformDirty = true;
        }
    }

    private void RefreshCurrentImageSequenceSourceAfterCrossPageToggle()
    {
        // Reset image cache to avoid mixing different decode policies
        // between cross-page and single-page rendering.
        ClearNeighborImageCache();
        var currentPage = GetCurrentPageIndexForCrossPage();
        var bitmap = GetPageBitmap(currentPage);
        if (bitmap == null)
        {
            return;
        }

        PhotoBackground.Source = bitmap;
        RefreshPhotoBackgroundVisibility();
        UpdateCurrentPageWidthNormalization(bitmap);
    }

    private void ReloadPdfInkCacheAfterCrossPageToggle()
    {
        SaveCurrentPageOnNavigate(forceBackground: false);
        _currentCacheKey = BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, isPdf: true);
        ResetInkHistory();
        LoadCurrentPageIfExists();
    }

    private void ApplyLoadedBitmapTransform(BitmapSource bitmap, bool useCrossPageUnifiedPath)
    {
        var path = PhotoLoadedBitmapTransformPathPolicy.Resolve(
            useCrossPageUnifiedPath,
            _rememberPhotoTransform,
            _photoUnifiedTransformReady);
        switch (path)
        {
            case PhotoLoadedBitmapTransformPath.ApplyUnifiedTransform:
                ApplyLastUnifiedPhotoTransform(markUserDirty: false);
                return;
            case PhotoLoadedBitmapTransformPath.FitToViewport:
                ApplyPhotoFitToViewport(bitmap);
                return;
            case PhotoLoadedBitmapTransformPath.TryStoredTransformThenFit:
                var appliedStored = TryApplyStoredPhotoTransform(GetCurrentPhotoTransformKey());
                if (!appliedStored)
                {
                    ApplyPhotoFitToViewport(bitmap);
                }
                return;
            default:
                ApplyPhotoFitToViewport(bitmap);
                return;
        }
    }
}
