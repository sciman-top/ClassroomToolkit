using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Windowing;
using IoPath = System.IO.Path;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void NavigateToPage(
        int newPageIndex,
        double newTranslateY,
        bool interactiveSwitch = false,
        BitmapSource? preloadedBitmap = null,
        bool deferCrossPageDisplayUpdate = false,
        int? previousPageIndexForInteractiveSwitch = null,
        BitmapSource? previousPageBitmapForInteractiveSwitch = null,
        bool clearPreservedNeighborInkFrames = false)
    {
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "navigate-enter",
                $"targetPage={newPageIndex} interactive={interactiveSwitch}");
        }
        var beforeCurrentPage = GetCurrentPageIndexForCrossPage();
        if (_photoDocumentIsPdf)
        {
            _currentPageIndex = newPageIndex;
            _currentCacheKey = BuildPdfCacheKey(_currentDocumentPath, _currentPageIndex);
            ResetInkHistory();
            _photoTranslate.Y = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
                _photoTranslate.Y,
                newTranslateY,
                pageChanged: beforeCurrentPage != newPageIndex,
                photoInkModeActive: IsPhotoInkModeActive(),
                crossPageDisplayActive: IsCrossPageDisplayActive());
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-load-start", "doc=pdf");
            }
            LoadCurrentPageIfExists(
                allowDiskFallback: !interactiveSwitch,
                preferInteractiveFastPath: interactiveSwitch);
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-load-end", "doc=pdf");
            }
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-render-start", "doc=pdf");
            }
            if (!RenderPdfPage(_currentPageIndex, interactiveSwitch, preloadedBitmap))
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-render-failed", "doc=pdf");
                }
                return;
            }
            if (IsCrossPageFirstInputTraceActive())
            {
                MarkCrossPageFirstInputStage("navigate-render-end", "doc=pdf");
            }
        }
        else
        {
            if (newPageIndex <= 0 || newPageIndex > _photoSequencePaths.Count)
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-skip", $"invalid-page={newPageIndex}");
                }
                return;
            }

            _photoSequenceIndex = newPageIndex - 1;
            if (_photoSequenceIndex >= 0 && _photoSequenceIndex < _photoSequencePaths.Count)
            {
                var newPath = _photoSequencePaths[_photoSequenceIndex];
                if (ClassroomToolkit.Application.UseCases.Photos.PhotoNavigationPlanner.ClassifyPath(newPath)
                    != ClassroomToolkit.Application.UseCases.Photos.PhotoFileType.Image)
                {
                    if (IsCrossPageFirstInputTraceActive())
                    {
                        MarkCrossPageFirstInputStage("navigate-skip", "target-not-image");
                    }
                    return;
                }
                _currentDocumentName = IoPath.GetFileNameWithoutExtension(newPath);
                _currentDocumentPath = newPath;
                _currentCacheKey = BuildPhotoCacheKey(newPath);
                ResetInkHistory();
                _photoTranslate.Y = PhotoNavigationInkLoadTranslatePolicy.ResolveTranslateYBeforeLoad(
                    _photoTranslate.Y,
                    newTranslateY,
                    pageChanged: beforeCurrentPage != newPageIndex,
                    photoInkModeActive: IsPhotoInkModeActive(),
                    crossPageDisplayActive: IsCrossPageDisplayActive());
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-image-switch-start");
                }
                var newBitmap = CrossPageSwitchBitmapResolver.ResolveForInteractiveSwitch(
                    interactiveSwitch,
                    preloadedBitmap,
                    () => GetPageBitmap(newPageIndex));
                if (newBitmap != null)
                {
                    // Put the target page bitmap in place first, so interactive ink fast-path
                    // can reuse neighbor-rendered bitmap without forcing a full redraw.
                    PhotoBackground.Source = newBitmap;
                    RefreshPhotoBackgroundVisibility();
                    UpdateCurrentPageWidthNormalization(newBitmap);
                }
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage(
                        "navigate-image-switch-end",
                        $"bitmap={(newBitmap != null ? "hit" : "miss")}");
                }
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-load-start", "doc=image");
                }
                LoadCurrentPageIfExists(
                    allowDiskFallback: !interactiveSwitch,
                    preferInteractiveFastPath: interactiveSwitch);
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("navigate-load-end", "doc=image");
                }
                if (PhotoTitleText != null)
                {
                    PhotoTitleText.Text = IoPath.GetFileName(newPath);
                }
            }
        }
        // Apply new position
        _photoTranslate.Y = newTranslateY;
        var viewportSyncAction = PhotoNavigationInkViewportSyncPolicy.ResolveAction(
            IsPhotoInkModeActive(),
            interactiveSwitch);
        if (viewportSyncAction == PhotoNavigationInkViewportSyncAction.UpdatePanCompensation)
        {
            UpdatePhotoInkPanCompensation();
        }
        else if (viewportSyncAction == PhotoNavigationInkViewportSyncAction.ResetPanCompensation)
        {
            // Page switch loads a page-specific raster; carrying previous-page pan compensation
            // can shift the entire current page ink layer out of view until a later redraw.
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: true);
        }
        else
        {
            UpdatePhotoInkClip();
        }

        var currentPageAfterNavigation = GetCurrentPageIndexForCrossPage();
        var pageChanged = beforeCurrentPage != currentPageAfterNavigation;
        var previousPageForNeighborSeed = previousPageIndexForInteractiveSwitch.GetValueOrDefault(beforeCurrentPage);
        var preservedPageForMutationClear = CrossPageMutationNeighborRetentionPolicy.ResolvePreservedPage(
            clearPreservedNeighborInkFrames,
            pageChanged,
            previousPageForNeighborSeed,
            currentPageAfterNavigation);
        if (CrossPageNavigationCurrentInkRefreshPolicy.ShouldRequest(
                pageChanged,
                interactiveSwitch,
                IsPhotoInkModeActive(),
                _mode))
        {
            RequestPhotoTransformInkRedraw();
        }

        if (IsCrossPageDisplayActive())
        {
            if (clearPreservedNeighborInkFrames && pageChanged)
            {
                // Prevent old/new page ink carryover across a mutation-triggered seam switch.
                // RenderNeighborPages may preserve previous slot ink frames for continuity,
                // but this switch requires hard page ownership boundaries.
                ClearNeighborInkVisuals(
                    clearSlotIdentity: true,
                    preservePageIndex: preservedPageForMutationClear);
                if (CrossPageMutationNeighborSeedPolicy.ShouldSeedPreviousPageAfterClear(
                        clearPreservedNeighborInkFrames,
                        pageChanged,
                        previousPageForNeighborSeed,
                        currentPageAfterNavigation))
                {
                    // Re-seed previous page neighbor frame immediately after clear to avoid
                    // old-page self-ink one-frame flash during fast seam crossing.
                    TrySeedNeighborFrameForInteractiveSwitch(
                        previousPageForNeighborSeed,
                        previousPageBitmapForInteractiveSwitch);
                }
            }
            if (interactiveSwitch)
            {
                // Keep pointer-down path lightweight: full boundary computation walks all pages and
                // may synchronously load uncached bitmaps from disk, which causes first-stroke stalls.
                if (pageChanged)
                {
                    // For brush cross-page input, keep the target page's previous neighbor slot
                    // visible until the next formal cross-page refresh. This avoids a one-switch
                    // blank current page when the current raster has not yet been rehydrated.
                    if (CrossPageCurrentPageSeedSlotHidePolicy.ShouldHide(_mode))
                    {
                        HideNeighborSlotForPage(GetCurrentPageIndexForCrossPage());
                    }
                }
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("bounds-skip", "interactive-switch");
                }
            }
            else
            {
                ApplyCrossPageBoundaryLimits();
            }
        }
        else
        {
            // Single-page mode clamp
            var newBitmapSource = PhotoBackground.Source as BitmapSource;
            if (newBitmapSource != null)
            {
                var newPageHeight = GetScaledPageHeight(newBitmapSource);
                var minY = -(newPageHeight - OverlayRoot.ActualHeight * 0.1);
                var maxY = OverlayRoot.ActualHeight * 0.9;
                _photoTranslate.Y = Math.Clamp(_photoTranslate.Y, minY, maxY);
            }
        }
        SafeActionExecutionExecutor.TryExecute(
            () => InkContextChanged?.Invoke(_currentDocumentName, _currentCourseDate),
            ex => Debug.WriteLine($"[InkContextChanged] enter callback failed: {ex.GetType().Name} - {ex.Message}"));
        if (interactiveSwitch)
        {
            TrySeedNeighborFrameForInteractiveSwitch(
                previousPageForNeighborSeed,
                previousPageBitmapForInteractiveSwitch);

            var refreshMode = CrossPageInteractiveSwitchRefreshPolicy.Resolve(_mode, deferCrossPageDisplayUpdate);
            if (CrossPageDeferredRefreshPolicy.ShouldArmOnInteractiveSwitch(refreshMode))
            {
                _crossPageUpdateDeferredByInkInput = true;
            }
            else
            {
                if (IsCrossPageFirstInputTraceActive())
                {
                    MarkCrossPageFirstInputStage("crosspage-update-request");
                }
                if (refreshMode == CrossPageInteractiveSwitchRefreshMode.ImmediateDirect)
                {
                    RequestCrossPageDisplayUpdate(CrossPageUpdateSources.NavigateInteractiveBrush);
                }
                else
                {
                    var scheduled = TryBeginInvoke(
                        () => RequestCrossPageDisplayUpdate(CrossPageUpdateSources.NavigateInteractive),
                        DispatcherPriority.Background);
                    if (!scheduled)
                    {
                        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.NavigateInteractiveFallback);
                    }
                }
            }
        }
        else
        {
            UpdateCrossPageDisplay();
        }
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage(
                "navigate-exit",
                $"activePage={GetCurrentPageIndexForCrossPage()} bgVisible={PhotoBackground.Visibility}");
        }
    }
}
