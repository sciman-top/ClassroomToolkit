using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool TrySwitchActiveImagePageForInput(BrushInputSample input)
    {
        var interactionState = CaptureInputInteractionState();
        var position = input.Position;
        var currentBitmap = PhotoBackground.Source as BitmapSource;
        var hasBitmap = currentBitmap != null;
        var hasCurrentRect = hasBitmap && TryBuildImageScreenRect(currentBitmap!, _photoContentTransform, out var currentRectValue);
        Rect? currentRect = hasCurrentRect ? currentRectValue : null;
        if (!CrossPageInputSwitchRequestPolicy.ShouldSwitchForInput(
                interactionState.PhotoModeActive,
                interactionState.CrossPageDisplayEnabled,
                interactionState.BoardActive,
                _mode,
                _photoPanning,
                _crossPageDragging,
                hasBitmap,
                currentRect,
                position,
                pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip))
        {
            return false;
        }

        var resolvedBitmap = currentBitmap!;
        var currentPage = GetCurrentPageIndexForCrossPage();
        var targetPage = ResolveCrossPageTargetForInput(position.Y, currentPage, resolvedBitmap);
        var boundedTargetPage = CrossPageInputSwitchTargetPolicy.ResolveNeighborTargetPage(currentPage, targetPage);
        return SwitchToImagePageForInput(currentPage, boundedTargetPage, resolvedBitmap, preloadedBitmap: null, input);
    }

    private bool SwitchToImagePageForInput(
        int currentPage,
        int targetPage,
        BitmapSource currentBitmap,
        BitmapSource? preloadedBitmap,
        BrushInputSample input)
    {
        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentPageTop = _photoTranslate.Y;
        var currentPageHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        var executionPlan = CrossPageInputSwitchExecutionPolicy.Resolve(
            currentPage,
            targetPage,
            _mode,
            currentPageHeight);
        if (!executionPlan.ShouldSwitch)
        {
            return false;
        }

        var offset = CrossPageInputNavigation.ComputePageOffset(
            currentPage,
            targetPage,
            page => GetScaledHeightForPage(page, normalizedWidthDip));
        var targetTop = _photoTranslate.Y + offset;
        var viewportHeight = OverlayRoot.ActualHeight > 0 ? OverlayRoot.ActualHeight : ActualHeight;
        if (viewportHeight > 0)
        {
            var totalPages = GetTotalPageCount();
            var targetPageHeight = GetScaledHeightForInteractiveSwitchClamp(
                targetPage,
                normalizedWidthDip,
                fallbackHeight: currentPageHeight > 0
                    ? currentPageHeight
                    : PhotoTransformViewportDefaults.MinUsableViewportDip);
            targetTop = CrossPageInteractiveSwitchClampPolicy.ClampTranslateY(
                targetTop,
                targetPage,
                totalPages,
                viewportHeight,
                page => GetScaledHeightForInteractiveSwitchClamp(page, normalizedWidthDip, targetPageHeight),
                fallbackPageHeight: targetPageHeight);
        }
        var nowUtc = GetCurrentUtcTimestamp();
        var seamY = targetPage > currentPage
            ? currentPageTop + currentPageHeight
            : currentPageTop;
        if (CrossPageInputSwitchBounceGuardPolicy.ShouldSuppress(
                currentPage,
                targetPage,
                _lastInputSwitchFromPage,
                _lastInputSwitchToPage,
                _lastInputSwitchUtc,
                nowUtc,
                input.Position.Y,
                seamY,
                CrossPageInputSwitchThresholds.ReverseSwitchSeamBandDip,
                CrossPageInputSwitchThresholds.ReverseSwitchCooldownMs))
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent(
                "skip",
                "input-switch-bounce-guard",
                $"from={currentPage} to={targetPage}");
            return false;
        }

        BeginCrossPageFirstInputTrace(currentPage, targetPage);
        MarkCrossPageFirstInputStage("switch-resolved", $"offset={offset:F2} top={targetTop:F2}");

        // Cross-page pointer-down must stay lightweight to avoid first-stroke stalls.
        BrushInputSample? continuationSeed = _mode == PaintToolMode.Brush ? input : null;
        var replayCurrentInputAfterResume = false;
        CrossPageBrushContinuationPolicy.Decision? continuationDecision = null;
        if (executionPlan.ShouldResolveBrushContinuation)
        {
            continuationDecision = CrossPageBrushContinuationPolicy.Resolve(
                input,
                _lastBrushInputSample,
                currentPageTop,
                currentPageHeight,
                currentPage,
                targetPage);
            continuationSeed = continuationDecision.Value.ContinuationSeed;
            replayCurrentInputAfterResume = continuationDecision.Value.ShouldReplayCurrentInputAfterResume;
        }

        var previousPointerPosition = _lastPointerPosition;
        var switchTriggeredByActiveInkMutation = _strokeInProgress || _isErasing || _isRegionSelecting;
        if (continuationDecision.HasValue)
        {
            _lastPointerPosition = continuationDecision.Value.FinalizeSample.Position;
        }
        MarkCrossPageFirstInputStage("save-old-page-start");
        var previousSuppressCrossPageVisualSync = _suppressCrossPageVisualSync;
        _suppressCrossPageVisualSync = true;
        _suppressImmediatePhotoInkRedraw = true;
        try
        {
            SaveCurrentPageOnNavigate(
                forceBackground: false,
                persistToSidecar: false,
                finalizeActiveOperation: true);
            _lastPointerPosition = previousPointerPosition;
            MarkCrossPageFirstInputStage("save-old-page-end");
            MarkCrossPageFirstInputStage("navigate-start");
            var switchNavigationPlan = CrossPageInputSwitchNavigationPolicy.Resolve(
                _mode,
                _strokeInProgress,
                _isErasing,
                _isRegionSelecting,
                inputTriggeredByActiveInkMutation: switchTriggeredByActiveInkMutation);
            var clearPreservedNeighborInkFrames = CrossPageMutationNeighborInkCarryoverPolicy.ShouldClearPreservedNeighborInkFrames(
                pageChanged: currentPage != targetPage,
                interactiveSwitch: switchNavigationPlan.InteractiveSwitch,
                inputTriggeredByActiveInkMutation: switchTriggeredByActiveInkMutation,
                mode: _mode);
            NavigateToPage(
                targetPage,
                targetTop,
                interactiveSwitch: switchNavigationPlan.InteractiveSwitch,
                preloadedBitmap: preloadedBitmap,
                deferCrossPageDisplayUpdate: switchNavigationPlan.InteractiveSwitch
                    ? executionPlan.DeferCrossPageDisplayUpdate
                    : switchNavigationPlan.DeferCrossPageDisplayUpdate,
                previousPageIndexForInteractiveSwitch: currentPage,
                previousPageBitmapForInteractiveSwitch: currentBitmap,
                clearPreservedNeighborInkFrames: clearPreservedNeighborInkFrames);
            if (GetCurrentPageIndexForCrossPage() == targetPage)
            {
                _lastInputSwitchFromPage = currentPage;
                _lastInputSwitchToPage = targetPage;
                _lastInputSwitchUtc = nowUtc;
            }
            _pendingCrossPageBrushContinuationSample = continuationSeed;
            _pendingCrossPageBrushReplayCurrentInput = replayCurrentInputAfterResume;
            MarkCrossPageFirstInputStage("navigate-end", $"activePage={GetCurrentPageIndexForCrossPage()}");
        }
        finally
        {
            _lastPointerPosition = previousPointerPosition;
            _suppressImmediatePhotoInkRedraw = false;
            _suppressCrossPageVisualSync = previousSuppressCrossPageVisualSync;
        }
        return true;
    }

    private bool TryResolveVisibleImagePageFromPointer(
        WpfPoint pointer,
        int currentPage,
        out int pageIndex,
        out BitmapSource? resolvedBitmap)
    {
        pageIndex = currentPage;
        resolvedBitmap = null;
        if (!CaptureInputInteractionState().CrossPageDisplayActive)
        {
            return false;
        }

        var currentBitmap = PhotoBackground.Source as BitmapSource;
        var hasCurrentBitmap = currentBitmap != null;
        var hasCurrentRect = hasCurrentBitmap
            && TryBuildImageScreenRect(currentBitmap!, _photoContentTransform, out var currentRect);
        var pointerInsideCurrentRect = hasCurrentRect && currentRect.Contains(pointer);
        if (CrossPageCurrentPagePointerHitPolicy.ShouldUseCurrentPage(
                hasCurrentBitmap,
                hasCurrentRect,
                pointerInsideCurrentRect))
        {
            pageIndex = currentPage;
            resolvedBitmap = currentBitmap;
            return true;
        }

        foreach (var img in _neighborPageImages)
        {
            var bitmap = img.Source as BitmapSource;
            var hasBitmap = bitmap != null;
            var hasCandidatePage = int.TryParse(
                img.Uid,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var candidatePage);
            var hasRect = hasBitmap && TryBuildImageScreenRect(bitmap!, img.RenderTransform, out var rect);
            var pointerInsideRect = hasRect && rect.Contains(pointer);
            if (CrossPageNeighborPageCandidatePolicy.ShouldUseCandidate(
                    img.Visibility,
                    hasBitmap,
                    hasCandidatePage,
                    candidatePage,
                    currentPage,
                    hasRect,
                    pointerInsideRect))
            {
                pageIndex = candidatePage;
                resolvedBitmap = bitmap;
                return true;
            }
        }

        return false;
    }

    private bool ShouldContinuePointerInput(
        InputEventArgs e,
        bool hideEraserPreviewWhenBlocked = false)
    {
        var handlingPlan = ResolvePointerSourceHandlingPlan(e, hideEraserPreviewWhenBlocked);
        if (handlingPlan.ShouldHideEraserPreview)
        {
            HideEraserPreview();
        }

        if (handlingPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }

        return handlingPlan.ShouldContinue;
    }

    private OverlayPointerSourceHandlingPlan ResolvePointerSourceHandlingPlan(
        InputEventArgs e,
        bool hideEraserPreviewWhenBlocked = false)
    {
        var sourceGateDecision = OverlayPointerSourceGatePolicy.Resolve(
            _photoLoading,
            ShouldIgnoreInputFromPhotoControls(e.OriginalSource as DependencyObject));
        return OverlayPointerSourceHandlingPolicy.Resolve(
            sourceGateDecision,
            hideEraserPreviewWhenBlocked);
    }

    public bool CanRoutePresentationInput()
    {
        return CanRoutePresentationInputFromOverlay();
    }

    public bool CanRoutePresentationInputFromAuxWindow()
    {
        var interactionState = CaptureInputInteractionState();
        return OverlayPresentationRoutingPolicy.CanRouteFromAuxWindow(
            _sessionCoordinator.CurrentState.NavigationMode,
            interactionState.PhotoModeActive,
            interactionState.BoardActive);
    }

    public void UpdatePhotoPostInputRefreshDelayMs(int delayMs)
    {
        _photoPostInputRefreshDelayMs = CrossPagePostInputRefreshDelayClampPolicy.Clamp(delayMs);
    }

    public void UpdatePhotoInputTelemetryEnabled(bool enabled)
    {
        _photoInputTelemetryEnabled = enabled;
    }

    private bool TryBuildImageScreenRect(BitmapSource bitmap, Transform? transform, out Rect rect)
    {
        rect = Rect.Empty;
        if (bitmap == null || transform == null)
        {
            return false;
        }

        var width = GetBitmapDisplayWidthInDip(bitmap);
        var height = GetBitmapDisplayHeightInDip(bitmap);
        if (width <= InputGeometryDefaults.MinRenderableImageSideDip
            || height <= InputGeometryDefaults.MinRenderableImageSideDip)
        {
            return false;
        }

        var logicalRect = new Rect(0, 0, width, height);
        rect = Rect.Transform(logicalRect, transform.Value);
        return !rect.IsEmpty;
    }

    private int ResolveCrossPageTargetForInput(double pointerY, int currentPage, BitmapSource currentBitmap)
    {
        var totalPages = GetTotalPageCount();
        if (totalPages <= 1)
        {
            return currentPage;
        }

        var normalizedWidthDip = GetCrossPageNormalizedWidthDip(currentBitmap);
        var currentHeight = GetScaledPageHeight(currentBitmap, normalizedWidthDip);
        var currentTop = _photoTranslate.Y;
        return CrossPageInputNavigation.ResolveTargetPage(
            pointerY,
            currentPage,
            totalPages,
            currentTop,
            currentHeight,
            page => GetScaledHeightForPage(page, normalizedWidthDip));
    }
}
