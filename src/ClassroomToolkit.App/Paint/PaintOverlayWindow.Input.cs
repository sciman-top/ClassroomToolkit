using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Globalization;
using System.Diagnostics;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Session;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private StylusSampleTimestampState _stylusSampleTimestampState = StylusSampleTimestampState.Default;

    private bool IsWithinPhotoControls(DependencyObject? source)
    {
        if (source == null)
        {
            return false;
        }
        // Any visual under the photo control layer should not trigger drawing/panning hit logic.
        return IsDescendantOf(source, PhotoControlLayer) ||
               IsDescendantOf(source, PhotoLoadingOverlay);
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject? ancestor)
    {
        while (source != null)
        {
            if (ReferenceEquals(source, ancestor))
            {
                return true;
            }
            source = VisualTreeHelper.GetParent(source);
        }
        return false;
    }

    private bool ShouldIgnoreInputFromPhotoControls(DependencyObject? source)
    {
        return _photoModeActive && IsWithinPhotoControls(source);
    }

    private static bool IsPhotoNavigationKey(Key key, out int direction)
    {
        direction = 0;
        if (key == Key.Right || key == Key.Down || key == Key.PageDown || key == Key.Space || key == Key.Enter)
        {
            direction = 1;
            return true;
        }
        if (key == Key.Left || key == Key.Up || key == Key.PageUp)
        {
            direction = -1;
            return true;
        }
        return false;
    }

    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        var wheelRoute = OverlayInputRoutingPolicy.ResolveWheelRoute(
            interactionState.BoardActive,
            interactionState.PhotoModeActive,
            CanRoutePresentationInputFromOverlay(interactionState),
            PresentationChannelAvailabilityPolicy.IsAnyChannelEnabled(
                _presentationOptions.AllowOffice,
                _presentationOptions.AllowWps));
        if (wheelRoute == OverlayWheelInputRoute.ConsumeForBoard)
        {
            e.Handled = true;
            return;
        }
        if (wheelRoute == OverlayWheelInputRoute.HandlePhoto)
        {
            if (ShouldSuppressPhotoWheelFromRecentGesture())
            {
                LogPhotoInputTelemetry("wheel", "suppressed-recent-gesture");
                e.Handled = true;
                return;
            }
            ZoomPhoto(e.Delta, e.GetPosition(OverlayRoot));
            LogPhotoInputTelemetry("wheel", $"delta={e.Delta}");
            e.Handled = true;
            return;
        }
        if (wheelRoute != OverlayWheelInputRoute.RoutePresentation)
        {
            return;
        }
        var foregroundType = ResolveForegroundPresentationType();
        var presentationExecutionAction = OverlayWheelPresentationExecutionPolicy.Resolve(
            _wpsNavHookActive,
            _wpsHookInterceptWheel,
            _wpsHookBlockOnly,
            isWpsForeground: MapRouteType(foregroundType) == OverlayPresentationRouteType.Wps,
            WpsHookRecentlyFired(),
            e.Delta);
        var command = presentationExecutionAction switch
        {
            OverlayWheelPresentationExecutionAction.SendNext => ClassroomToolkit.Services.Presentation.PresentationCommand.Next,
            OverlayWheelPresentationExecutionAction.SendPrevious => ClassroomToolkit.Services.Presentation.PresentationCommand.Previous,
            _ => (ClassroomToolkit.Services.Presentation.PresentationCommand?)null
        };
        if (!command.HasValue)
        {
            return;
        }

        if (TrySendPresentationCommand(command.Value))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        var photoKeyHandled = TryHandlePhotoKey(e.Key);
        var keyRoute = OverlayInputRoutingPolicy.ResolveKeyRoute(
            _photoLoading,
            photoKeyHandled,
            interactionState.PhotoOrBoardActive,
            CanRoutePresentationInputFromOverlay(interactionState));
        if (keyRoute == OverlayKeyInputRoute.Consume)
        {
            e.Handled = true;
            return;
        }
        if (keyRoute != OverlayKeyInputRoute.RoutePresentation)
        {
            return;
        }
        if (TryHandlePresentationKey(e.Key))
        {
            e.Handled = true;
        }
    }

    private bool CanRoutePresentationInputFromOverlay()
    {
        return CanRoutePresentationInputFromOverlay(CaptureInputInteractionState());
    }

    private bool CanRoutePresentationInputFromOverlay(InputInteractionState interactionState)
    {
        return OverlayPresentationRoutingPolicy.CanRouteFromOverlay(
            _sessionCoordinator.CurrentState.NavigationMode,
            interactionState.PhotoModeActive,
            interactionState.BoardActive,
            _mode,
            _inputPassthroughEnabled);
    }

    public bool TryHandlePhotoKey(Key key)
    {
        var interactionState = CaptureInputInteractionState();
        if (!interactionState.PhotoNavigationEnabled)
        {
            return false;
        }
        if (key == Key.Escape && _photoFullscreen)
        {
            _photoFullscreen = false;
            SetPhotoWindowMode(fullscreen: false);
            return true;
        }
        if (IsPhotoNavigationKey(key, out var direction))
        {
            PhotoNavigationDiagnostics.Log("Overlay.Key", $"key={key}, dir={direction}, isPdf={_photoDocumentIsPdf}");
            HandlePhotoNavigationRequest(direction);
            return true;
        }
        if (key == Key.Add || key == Key.OemPlus)
        {
            ZoomPhotoByFactor(PhotoKeyZoomStep);
            return true;
        }
        if (key == Key.Subtract || key == Key.OemMinus)
        {
            ZoomPhotoByFactor(1.0 / PhotoKeyZoomStep);
            return true;
        }
        return false;
    }


    private void OnMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!ShouldContinuePointerInput(e))
        {
            return;
        }
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }
        if (TryBeginPhotoPan(e))
        {
            return;
        }
        var position = e.GetPosition(OverlayRoot);
        HandlePointerDown(position);
        e.Handled = true;
    }

    private void OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!ShouldContinuePointerInput(e, hideEraserPreviewWhenBlocked: true))
        {
            return;
        }
        var interactionState = CaptureInputInteractionState();
        var position = e.GetPosition(OverlayRoot);
        _lastPointerPosition = position;
        if (TryHandleMousePhotoPanMove(e, position, interactionState)) return;
        if (e.LeftButton != System.Windows.Input.MouseButtonState.Pressed)
        {
            return;
        }
        HandlePointerMove(position);
        e.Handled = true;
    }

    private void OnMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!ShouldContinuePointerInput(e))
        {
            return;
        }
        if (TryHandleMousePhotoPanEnd(e, CaptureInputInteractionState())) return;
        var position = e.GetPosition(OverlayRoot);
        HandlePointerUp(position);
        e.Handled = true;
    }

    private void OnRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!ShouldContinuePointerInput(e))
        {
            return;
        }
        var interactionState = CaptureInputInteractionState();
        var shouldArmPending = PhotoRightClickContextMenuPolicy.ShouldArmPending(
            interactionState.PhotoModeActive,
            _photoFullscreen,
            _mode);
        var downExecutionPlan = PhotoRightButtonDownExecutionPolicy.Resolve(
            shouldArmPending,
            shouldAllowPan: ResolveShouldPanPhoto(interactionState));
        if (downExecutionPlan.ShouldArmPending)
        {
            PhotoRightClickPendingStateUpdater.Arm(
                ref _photoRightClickPending,
                ref _photoRightClickStart,
                e.GetPosition(OverlayRoot));
        }
        if (!downExecutionPlan.ShouldTryBeginPan)
        {
            return;
        }
        if (TryBeginPhotoPan(e))
        {
            return;
        }
    }

    private void OnRightButtonMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!ShouldContinuePointerInput(e))
        {
            return;
        }
        var interactionState = CaptureInputInteractionState();
        UpdatePhotoRightClickPendingByMove(e.GetPosition(OverlayRoot));
        TryHandleMousePhotoPanMove(e, e.GetPosition(OverlayRoot), interactionState);
    }

    private void OnOverlayLostMouseCapture(object sender, System.Windows.Input.MouseEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        var lostCapturePlan = OverlayLostMouseCaptureExecutionPolicy.Resolve(
            IsMousePhotoPanActive(interactionState),
            rightClickPending: _photoRightClickPending);
        if (lostCapturePlan.ShouldEndPan)
        {
            EndPhotoPan();
        }
        if (lostCapturePlan.ShouldClearRightClickPending)
        {
            PhotoRightClickPendingStateUpdater.Clear(ref _photoRightClickPending);
        }
    }

    private void OnOverlayMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        HideEraserPreview();
    }

    private void OnRightButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (!ShouldContinuePointerInput(e))
        {
            return;
        }
        var interactionState = CaptureInputInteractionState();
        if (TryHandlePhotoRightClickContextMenuOnUp(e.GetPosition(OverlayRoot), e, interactionState)) return;
        TryHandleMousePhotoPanEnd(e, interactionState);
    }

    private void UpdatePhotoRightClickPendingByMove(WpfPoint point)
    {
        PhotoRightClickPendingStateUpdater.UpdateByMove(
            ref _photoRightClickPending,
            _photoRightClickStart,
            point);
    }

    private bool TryHandlePhotoRightClickContextMenuOnUp(
        WpfPoint point,
        System.Windows.Input.MouseButtonEventArgs e,
        InputInteractionState interactionState)
    {
        var executionPlan = PhotoRightButtonUpExecutionPolicy.Resolve(
            PhotoRightClickContextMenuPolicy.ShouldShowContextMenuOnUp(
                _photoRightClickPending,
                interactionState.PhotoModeActive,
                _photoFullscreen,
                _mode));
        if (executionPlan.Action != PhotoRightButtonUpAction.ShowContextMenu)
        {
            return false;
        }

        if (executionPlan.ShouldClearPending)
        {
            PhotoRightClickPendingStateUpdater.Clear(ref _photoRightClickPending);
        }
        if (IsMousePhotoPanActive(interactionState))
        {
            EndPhotoPan();
        }
        ShowPhotoContextMenu(point);
        e.Handled = executionPlan.ShouldMarkHandled;
        return true;
    }

    private bool IsMousePhotoPanActive(InputInteractionState interactionState)
    {
        return PhotoPanMouseRoutingPolicy.ShouldHandlePhotoPan(
            _photoPanning,
            interactionState.PhotoModeActive,
            _mode,
            IsInkOperationActive());
    }

    private bool ResolveShouldPanPhoto(InputInteractionState interactionState)
    {
        return StylusCursorPolicy.ShouldPanPhoto(
            interactionState.PhotoModeActive,
            interactionState.BoardActive,
            _mode,
            IsInkOperationActive());
    }

    private bool TryHandleMousePhotoPanMove(
        System.Windows.Input.MouseEventArgs e,
        WpfPoint position,
        InputInteractionState interactionState)
    {
        var shouldAllowPhotoPan = ResolveShouldPanPhoto(interactionState);
        var decision = PhotoPanMouseMoveRoutingPolicy.Resolve(
            _photoPanning,
            shouldAllowPhotoPan,
            e.LeftButton,
            e.RightButton);
        var executionPlan = PhotoPanMouseExecutionPolicy.ResolveMove(decision);
        if (executionPlan.Action == PhotoPanMouseExecutionAction.PassThrough)
        {
            return false;
        }
        if (executionPlan.Action == PhotoPanMouseExecutionAction.EndPan)
        {
            EndPhotoPan();
            e.Handled = executionPlan.ShouldMarkHandled;
            return true;
        }

        UpdatePhotoPan(position);
        e.Handled = executionPlan.ShouldMarkHandled;
        return true;
    }

    private bool TryHandleMousePhotoPanEnd(
        System.Windows.Input.MouseButtonEventArgs e,
        InputInteractionState interactionState)
    {
        var shouldAllowPhotoPan = ResolveShouldPanPhoto(interactionState);
        var shouldEndPan = PhotoPanTerminationPolicy.ShouldEndPan(
            shouldAllowPhotoPan,
            e.LeftButton,
            e.RightButton);
        var executionPlan = PhotoPanMouseExecutionPolicy.ResolveEnd(
            _photoPanning,
            shouldEndPan);
        if (executionPlan.Action == PhotoPanMouseExecutionAction.PassThrough)
        {
            return false;
        }

        EndPhotoPan();
        e.Handled = executionPlan.ShouldMarkHandled;
        return true;
    }

    private void HandlePointerDown(WpfPoint position)
    {
        HandlePointerDown(BrushInputSample.CreatePointer(position));
    }

    private void HandlePointerDown(BrushInputSample input)
    {
        var position = input.Position;
        MarkInkInput();
        _lastPointerPosition = position;
        TrySwitchActiveImagePageForInput(input);
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("tool-dispatch", $"tool={_mode}");
        }
        // 设置正在绘图状态
        PaintModeManager.Instance.IsDrawing = true;

        HandlePointerDownByTool(input);
    }

    private void HandlePointerDownByTool(BrushInputSample input)
    {
        var position = input.Position;
        var executionPlan = PointerDownToolExecutionPolicy.Resolve(_mode);
        var handled = true;
        switch (executionPlan.Action)
        {
            case PointerDownToolAction.BeginRegionSelection:
                BeginRegionSelection(position);
                break;
            case PointerDownToolAction.BeginEraser:
                BeginEraser(position);
                break;
            case PointerDownToolAction.BeginShape:
                BeginShape(position);
                break;
            case PointerDownToolAction.BeginBrushStroke:
                BeginBrushStroke(input);
                break;
            default:
                handled = false;
                break;
        }
        if (handled && executionPlan.ShouldCapturePointer)
        {
            CapturePointerInput();
        }
    }

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

    private void HandlePointerMove(WpfPoint position)
    {
        HandlePointerMove(BrushInputSample.CreatePointer(position));
    }

    private void HandlePointerMove(BrushInputSample input)
    {
        var position = input.Position;
        MarkInkInput();
        _lastPointerPosition = position;
        var switchedPage = TrySwitchActiveImagePageForInput(input);
        ResumeCrossPageInputOperationAfterSwitch(switchedPage, input);
        if (ShouldSuppressCrossPageOutOfPageBrushMove(input, switchedPage))
        {
            _inkDiagnostics?.OnCrossPageUpdateEvent(
                "skip",
                "out-of-page-brush-move",
                $"page={GetCurrentPageIndexForCrossPage()} y={position.Y:0.##}");
            return;
        }
        HandlePointerMoveByTool(input);
    }

    private bool ShouldSuppressCrossPageOutOfPageBrushMove(BrushInputSample input, bool switchedPageThisFrame)
    {
        if (PhotoBackground.Source is not BitmapSource currentBitmap)
        {
            return false;
        }
        if (!TryBuildImageScreenRect(currentBitmap, _photoContentTransform, out var currentPageRect))
        {
            return false;
        }

        currentPageRect.Inflate(
            CrossPageInputSwitchThresholds.OutOfPageMoveSuppressMarginDip,
            CrossPageInputSwitchThresholds.OutOfPageMoveSuppressMarginDip);
        var pointerInsideCurrentPageRect = currentPageRect.Contains(input.Position);
        var recentSwitchGraceActive = false;
        if (_lastInputSwitchUtc != CrossPageRuntimeDefaults.UnsetTimestampUtc)
        {
            var elapsedMs = (GetCurrentUtcTimestamp() - _lastInputSwitchUtc).TotalMilliseconds;
            recentSwitchGraceActive = elapsedMs >= 0
                && elapsedMs <= CrossPageInputSwitchThresholds.OutOfPageMoveSuppressPostSwitchGraceMs;
        }
        return CrossPageOutOfPageMoveSuppressionPolicy.ShouldSuppress(
            crossPageDisplayActive: IsCrossPageDisplayActive(),
            mode: _mode,
            strokeInProgress: _strokeInProgress,
            switchedPageThisFrame: switchedPageThisFrame,
            recentSwitchGraceActive: recentSwitchGraceActive,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: pointerInsideCurrentPageRect);
    }

    private void HandlePointerMoveByTool(BrushInputSample input)
    {
        var position = input.Position;
        var action = PointerMoveToolExecutionPolicy.Resolve(_mode);
        switch (action)
        {
            case PointerMoveToolAction.UpdateBrushStroke:
                UpdateBrushStroke(input);
                return;
            case PointerMoveToolAction.UpdateEraser:
                UpdateEraser(position);
                return;
            case PointerMoveToolAction.UpdateRegionSelection:
                UpdateRegionSelection(position);
                return;
            case PointerMoveToolAction.UpdateShapePreview:
                UpdateShapePreview(position);
                return;
            default:
                return;
        }
    }

    private void HandlePointerUp(WpfPoint position)
    {
        HandlePointerUp(BrushInputSample.CreatePointer(position));
    }

    private void HandlePointerUp(BrushInputSample input)
    {
        var position = input.Position;
        var hadInkOperation = IsInkOperationActive();
        MarkInkInput();
        _lastPointerPosition = position;
        if (IsCrossPageFirstInputTraceActive())
        {
            MarkCrossPageFirstInputStage("tool-end", $"tool={_mode}");
        }
        HandlePointerUpByTool(input);
        ReleasePointerInput();
        var pointerUpState = CrossPagePointerUpStatePolicy.Resolve(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled());
        var crossPageDisplayActive = pointerUpState.CrossPageDisplayActive;
        var deferredState = CrossPagePointerUpDeferredStatePolicy.Resolve(
            deferredByInkInput: _crossPageUpdateDeferredByInkInput,
            crossPageDisplayActive: pointerUpState.CrossPageDisplayActive);
        _crossPageUpdateDeferredByInkInput = deferredState.NextDeferredByInkInput;
        var deferredRefreshRequested = deferredState.DeferredRefreshRequested;
        if (deferredState.ShouldLogStableRecover)
        {
            // Keep first-stroke path lightweight, but do not permanently skip the seam refresh.
            // A short delayed refresh after pointer-up restores neighbor page/ink visibility.
            _inkDiagnostics?.OnCrossPageUpdateEvent("defer", "pointer-up", "stable-recover-v3");
        }
        var pointerUpDecision = CrossPagePointerUpDecisionPolicy.Resolve(
            crossPageDisplayActive: crossPageDisplayActive,
            hadInkOperation: hadInkOperation,
            deferredRefreshRequested: deferredRefreshRequested,
            updatePending: _crossPageDisplayUpdateState.Pending);
        var pointerUpPlan = CrossPagePointerUpExecutionPlanPolicy.Resolve(
            pointerUpDecision,
            hadInkOperation,
            _pendingInkContextCheck);
        var postExecutionPlan = CrossPagePointerUpPostExecutionPolicy.Resolve(
            pointerUpPlan,
            IsCrossPageFirstInputTraceActive());
        if (postExecutionPlan.ShouldTrackPointerUp)
        {
            _lastCrossPagePointerUpUtc = GetCurrentUtcTimestamp();
            System.Threading.Interlocked.Increment(ref _crossPagePointerUpSequence);
        }
        if (postExecutionPlan.ShouldScheduleDeferredRefresh)
        {
            // Keep current/neighbor transforms coherent on pointer-up, and ensure at least one
            // post-input cross-page update to flush previous-page ink visibility.
            if (postExecutionPlan.ShouldApplyFastRefresh)
            {
                ApplyCrossPagePointerUpFastRefresh(requestImmediateRefresh: pointerUpDecision.ShouldRequestImmediateRefresh);
            }
            ScheduleCrossPageDisplayUpdateAfterInputSettles(
                source: postExecutionPlan.DeferredRefreshSource,
                singlePerPointerUp: true,
                delayOverrideMs: _photoPostInputRefreshDelayMs);
        }
        if (postExecutionPlan.ShouldFlushReplay)
        {
            TryFlushCrossPageReplayAfterPointerUp();
        }
        if (postExecutionPlan.ShouldEndFirstInputTrace)
        {
            EndCrossPageFirstInputTrace("pointer-up");
        }
        if (postExecutionPlan.ShouldRequestInkContextRefresh)
        {
            _pendingInkContextCheck = false;
            _refreshOrchestrator.RequestRefresh("pointer-up");
        }
    }

    private void HandlePointerUpByTool(BrushInputSample input)
    {
        var position = input.Position;
        var executionPlan = PointerUpToolExecutionPolicy.Resolve(
            _mode,
            _pendingAdaptiveRendererRefresh);
        switch (executionPlan.Action)
        {
            case PointerUpToolAction.EndBrushStroke:
                EndBrushStroke(input);
                if (executionPlan.ShouldRefreshAdaptiveRenderer)
                {
                    _pendingAdaptiveRendererRefresh = false;
                    EnsureActiveRenderer(force: true);
                }
                return;
            case PointerUpToolAction.EndEraser:
                EndEraser(position);
                return;
            case PointerUpToolAction.EndRegionSelection:
                EndRegionSelection(position);
                return;
            case PointerUpToolAction.EndShape:
                EndShape(position);
                return;
            default:
                return;
        }
    }

    private void OnManipulationStarting(object? sender, ManipulationStartingEventArgs e)
    {
        if (!TryAdmitPhotoManipulation(e, CaptureInputInteractionState()))
        {
            _photoManipulating = false;
            return;
        }
        _photoManipulating = true;
        e.ManipulationContainer = OverlayRoot;
        e.Mode = ManipulationModes.Scale | ManipulationModes.Translate;
    }

    private void OnManipulationDelta(object? sender, ManipulationDeltaEventArgs e)
    {
        var interactionState = CaptureInputInteractionState();
        if (!TryAdmitPhotoManipulation(e, interactionState))
        {
            _photoManipulating = false;
            return;
        }
        _photoManipulating = true;
        // Re-check at delta time to prevent gesture pan from racing with active ink operations.
        if (IsInkOperationActive())
        {
            e.Handled = true;
            return;
        }
        EnsurePhotoTransformsWritable();
        MarkPhotoGestureInput();
        var scale = e.DeltaManipulation.Scale;
        var factor = (scale.X + scale.Y) / 2.0;
        ApplyPhotoZoomInput(PhotoZoomInputSource.Gesture, factor, e.ManipulationOrigin);
        LogPhotoInputTelemetry("gesture-zoom", $"factor={factor:0.####}");
        var translation = e.DeltaManipulation.Translation;
        var deltaExecutionPlan = PhotoManipulationDeltaExecutionPolicy.Resolve(
            translation,
            PhotoZoomInputDefaults.ManipulationTranslationEpsilonDip,
            interactionState.CrossPageDisplayActive);
        if (deltaExecutionPlan.ShouldApplyTranslation)
        {
            _photoTranslate.X += translation.X;
            _photoTranslate.Y += translation.Y;
            ApplyPhotoPanBounds(allowResistance: true);
            UpdatePhotoInkPanCompensation();
            var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
                _lastPhotoInteractiveRefreshTranslateX,
                _lastPhotoInteractiveRefreshTranslateY,
                _photoTranslate.X,
                _photoTranslate.Y);
            SchedulePhotoTransformSave(userAdjusted: true);
            if (shouldRefresh)
            {
                SyncPhotoInteractiveRefreshAnchor();
                UpdateNeighborTransformsForPan();
                if (PhotoInkPanRedrawPolicy.ShouldRequest(
                        IsPhotoInkModeActive(),
                        _photoTranslate.X,
                        _photoTranslate.Y,
                        _lastInkRedrawPhotoTranslateX,
                        _lastInkRedrawPhotoTranslateY))
                {
                    RequestPhotoTransformInkRedraw();
                }
            }
            if (deltaExecutionPlan.ShouldLogPanTelemetry)
            {
                LogPhotoInputTelemetry("gesture-pan", $"dx={translation.X:0.##},dy={translation.Y:0.##}");
            }
        }
        if (deltaExecutionPlan.ShouldRequestCrossPageUpdate)
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ManipulationDelta);
        }
    }

    private void OnManipulationCompleted(object? sender, ManipulationCompletedEventArgs e)
    {
        _photoManipulating = false;
        if (!TryAdmitPhotoManipulation(e, CaptureInputInteractionState()))
        {
            return;
        }

        ApplyPhotoPanBounds(allowResistance: false);
        if (IsCrossPageDisplayActive())
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.WithImmediate(CrossPageUpdateSources.ManipulationDelta));
        }
        SchedulePhotoTransformSave(userAdjusted: true);
    }

    private bool TryAdmitPhotoManipulation(
        InputEventArgs e,
        InputInteractionState interactionState)
    {
        var handlingPlan = PhotoManipulationAdmissionPolicy.Resolve(
            interactionState.PhotoModeActive,
            interactionState.BoardActive,
            _mode,
            IsInkOperationActive(),
            _photoPanning);
        if (handlingPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }

        return handlingPlan.ShouldHandle;
    }

    private void ResumeCrossPageInputOperationAfterSwitch(bool switchedPage, BrushInputSample input)
    {
        var pendingSeed = _pendingCrossPageBrushContinuationSample;
        var replayCurrentInput = _pendingCrossPageBrushReplayCurrentInput;
        var seed = pendingSeed ?? input;
        var executionPlan = CrossPageInputResumePolicy.Resolve(
            switchedPage,
            _mode,
            _strokeInProgress,
            _isErasing,
            replayCurrentInput,
            pendingSeed.HasValue,
            seed == input);
        if (executionPlan.ShouldClearPendingBrushState)
        {
            _pendingCrossPageBrushContinuationSample = null;
            _pendingCrossPageBrushReplayCurrentInput = false;
        }

        if (executionPlan.Action == CrossPageInputResumeAction.BeginBrushContinuation)
        {
            // Cross-page resume can carry a seam seed from the previous frame.
            // Skip seed-only preview draw to avoid one-frame page-cross flash.
            _visualHost.Clear();
            BeginBrushStrokeContinuation(seed, renderInitialPreview: false);
            // Keep the current input sample update in the normal pointer-move path only.
            // This avoids a cross-page switch frame doing two preview updates with the same sample.
            return;
        }

        if (executionPlan.Action == CrossPageInputResumeAction.BeginEraser)
        {
            BeginEraser(input.Position);
        }
    }

    private BrushInputSample CreateStylusInputSample(StylusPoint stylusPoint)
    {
        return CreateStylusInputSample(stylusPoint, Stopwatch.GetTimestamp());
    }

    private BrushInputSample CreateStylusInputSample(StylusPoint stylusPoint, long timestampTicks)
    {
        if (timestampTicks <= 0)
        {
            timestampTicks = Stopwatch.GetTimestamp();
        }
        var position = new WpfPoint(stylusPoint.X, stylusPoint.Y);
        var orientation = StylusOrientationResolver.Resolve(stylusPoint);
        if (!_stylusPressureAnalyzer.TryResolve(
                stylusPoint.PressureFactor,
                _stylusPseudoPressureLowThreshold,
                _stylusPseudoPressureHighThreshold,
                ResolveStylusPressureGamma(_classroomWritingMode),
                out var pressure))
        {
            // Some classroom all-in-one touch devices report fixed 0/1 pseudo-pressure.
            // Treat as unavailable to keep velocity model stable.
            return BrushInputSample.CreatePointer(
                position,
                timestampTicks,
                orientation.AzimuthRadians,
                orientation.AltitudeRadians,
                orientation.TiltXRadians,
                orientation.TiltYRadians);
        }

        pressure = _stylusPressureCalibrator.Calibrate(pressure, _stylusPressureAnalyzer.Profile);
        if (_stylusDeviceAdaptiveProfiler.Observe(timestampTicks, _stylusPressureAnalyzer.Profile))
        {
            if (_strokeInProgress)
            {
                _pendingAdaptiveRendererRefresh = true;
            }
            else
            {
                EnsureActiveRenderer(force: true);
            }
            _brushPredictionHorizonMs = _stylusDeviceAdaptiveProfiler.CurrentProfile.PredictionHorizonMs;
        }

        return BrushInputSample.CreateStylus(
            position,
            timestampTicks,
            pressure,
            orientation.AzimuthRadians,
            orientation.AltitudeRadians,
            orientation.TiltXRadians,
            orientation.TiltYRadians);
    }

    private static double ResolveStylusPressureGamma(ClassroomWritingMode mode)
    {
        return mode switch
        {
            ClassroomWritingMode.Stable => StylusRuntimeDefaults.PressureGammaStable,
            ClassroomWritingMode.Responsive => StylusRuntimeDefaults.PressureGammaResponsive,
            _ => StylusRuntimeDefaults.PressureGammaDefault
        };
    }

    private void MarkPhotoGestureInput()
    {
        _lastPhotoGestureInputUtc = GetCurrentUtcTimestamp();
    }

    private bool ShouldSuppressPhotoWheelFromRecentGesture()
    {
        return PhotoInputConflictGuard.ShouldSuppressWheelAfterGesture(
            _lastPhotoGestureInputUtc,
            PhotoWheelSuppressAfterGestureMs,
            GetCurrentUtcTimestamp());
    }

    private void LogPhotoInputTelemetry(string eventType, string payload)
    {
        if (!_photoInputTelemetryEnabled)
        {
            return;
        }
        Debug.WriteLine(
            $"[PhotoInputTelemetry] type={eventType}; {payload}; " +
            $"scale={_photoScale.ScaleX:0.###}; tx={_photoTranslate.X:0.##}; ty={_photoTranslate.Y:0.##}");
    }

    private void OnStylusDown(object sender, StylusDownEventArgs e)
    {
        var handledByPhotoPan = !_photoLoading && TryHandleStylusPhotoPan(e, StylusPhotoPanPhase.Down);
        var shouldIgnoreFromPhotoControls = ShouldIgnoreInputFromPhotoControls(e.OriginalSource as DependencyObject);
        var stylusPoints = e.GetStylusPoints(OverlayRoot);
        var executionPlan = StylusDownExecutionPolicy.Resolve(
            _photoLoading,
            handledByPhotoPan,
            shouldIgnoreFromPhotoControls,
            stylusPoints.Count > 0);
        if (executionPlan.Action == StylusDownExecutionAction.None)
        {
            return;
        }

        if (executionPlan.ShouldResetTimestampState)
        {
            StylusSampleTimestampStateUpdater.Reset(ref _stylusSampleTimestampState);
        }

        switch (executionPlan.Action)
        {
            case StylusDownExecutionAction.HandleFirstStylusPoint:
            {
                long nowTicks = Stopwatch.GetTimestamp();
                var input = CreateStylusInputSample(stylusPoints[0], nowTicks);
                RememberStylusSampleTimestamp(input.TimestampTicks);
                HandlePointerDown(input);
                break;
            }
            case StylusDownExecutionAction.HandlePointerPosition:
                HandlePointerDown(e.GetPosition(OverlayRoot));
                break;
        }

        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }
    }

    private void OnStylusMove(object sender, StylusEventArgs e)
    {
        var handledByPhotoPan = !_photoLoading && TryHandleStylusPhotoPan(e, StylusPhotoPanPhase.Move);
        var stylusPoints = e.GetStylusPoints(OverlayRoot);
        var interactionState = CaptureInputInteractionState();
        var executionPlan = StylusMoveExecutionPolicy.Resolve(
            _photoLoading,
            handledByPhotoPan,
            IsInkOperationActive(),
            stylusPoints.Count > 0,
            _mode,
            _strokeInProgress,
            interactionState.CrossPageDisplayActive);
        switch (executionPlan.Action)
        {
            case StylusMoveExecutionAction.None:
                return;
            case StylusMoveExecutionAction.HandlePointerPosition:
                HandlePointerMove(e.GetPosition(OverlayRoot));
                break;
            case StylusMoveExecutionAction.HandleBrushBatch:
                HandleStylusBrushMoveBatch(stylusPoints);
                break;
            case StylusMoveExecutionAction.HandleStylusPointsIndividually:
                foreach (var stylusPoint in stylusPoints)
                {
                    HandlePointerMove(CreateStylusInputSample(stylusPoint));
                }
                break;
        }

        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }
    }

    private void HandleStylusBrushMoveBatch(StylusPointCollection stylusPoints)
    {
        MarkInkInput();
        long nowTicks = Stopwatch.GetTimestamp();
        long spanTicks = ResolveStylusBatchSpanTicks(nowTicks, stylusPoints.Count);
        long stepTicks = StylusBatchDispatchPolicy.ResolveStepTicks(spanTicks, stylusPoints.Count);
        long batchStartTicks = StylusBatchDispatchPolicy.ResolveBatchStartTicks(nowTicks, stepTicks, stylusPoints.Count);
        BrushInputSample? lastChangedSample = null;
        BrushInputSample? previousSample = (_lastPointerPosition.HasValue && _stylusSampleTimestampState.HasTimestamp)
            ? BrushInputSample.CreatePointer(_lastPointerPosition.Value, _stylusSampleTimestampState.LastTimestampTicks)
            : null;

        for (int index = 0; index < stylusPoints.Count; index++)
        {
            var stylusPoint = stylusPoints[index];
            long timestampTicks = EnsureMonotonicStylusTimestamp(batchStartTicks + (stepTicks * index));
            var sample = CreateStylusInputSample(stylusPoint, timestampTicks);
            if (previousSample.HasValue)
            {
                AppendInterpolatedBrushSamples(previousSample.Value, sample, ref lastChangedSample);
            }

            _lastPointerPosition = sample.Position;
            if (TryUpdateBrushStrokeGeometry(sample))
            {
                lastChangedSample = sample;
            }
            previousSample = sample;
            RememberStylusSampleTimestamp(sample.TimestampTicks);
        }

        if (lastChangedSample.HasValue)
        {
            FlushBrushStrokePreview(lastChangedSample.Value);
        }
    }

    private void OnStylusUp(object sender, StylusEventArgs e)
    {
        var handledByPhotoPan = !_photoLoading && TryHandleStylusPhotoPan(e, StylusPhotoPanPhase.Up);
        var stylusPoints = e.GetStylusPoints(OverlayRoot);
        var executionPlan = StylusUpExecutionPolicy.Resolve(
            _photoLoading,
            handledByPhotoPan,
            IsInkOperationActive(),
            stylusPoints.Count > 0);
        switch (executionPlan.Action)
        {
            case StylusUpExecutionAction.None:
                return;
            case StylusUpExecutionAction.HandleLastStylusPoint:
            {
                long nowTicks = Stopwatch.GetTimestamp();
                long timestampTicks = EnsureMonotonicStylusTimestamp(nowTicks);
                var input = CreateStylusInputSample(stylusPoints[^1], timestampTicks);
                RememberStylusSampleTimestamp(input.TimestampTicks);
                HandlePointerUp(input);
                break;
            }
            case StylusUpExecutionAction.HandlePointerPosition:
                HandlePointerUp(e.GetPosition(OverlayRoot));
                break;
        }

        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }
    }

    private bool TryHandleStylusPhotoPan(StylusEventArgs e, StylusPhotoPanPhase phase)
    {
        var interactionState = CaptureInputInteractionState();
        var shouldPanPhoto = ResolveShouldPanPhoto(interactionState);
        var panDecision = StylusPhotoPanRoutingPolicy.Resolve(
            shouldPanPhoto,
            _photoPanning,
            phase);

        var pointerSourcePlan = panDecision == StylusPhotoPanRoutingDecision.BeginPan
            ? ResolvePointerSourceHandlingPlan(e)
            : new OverlayPointerSourceHandlingPlan(
                ShouldContinue: true,
                ShouldMarkHandled: false,
                ShouldHideEraserPreview: false);
        var executionPlan = StylusPhotoPanExecutionPolicy.Resolve(
            panDecision,
            pointerSourcePlan.ShouldContinue,
            pointerSourcePlan.ShouldMarkHandled,
            PhotoPanBeginGuardPolicy.ShouldBegin(shouldPanPhoto, _photoPanning));
        if (executionPlan.ShouldMarkHandled)
        {
            e.Handled = true;
        }

        switch (executionPlan.Action)
        {
            case StylusPhotoPanExecutionAction.PassThrough:
                return false;
            case StylusPhotoPanExecutionAction.ReturnWithoutPan:
                return true;
            case StylusPhotoPanExecutionAction.BeginPan:
                BeginPhotoPan(e.GetPosition(OverlayRoot), captureStylus: true);
                return true;
            case StylusPhotoPanExecutionAction.UpdatePan:
                UpdatePhotoPan(e.GetPosition(OverlayRoot));
                return true;
            case StylusPhotoPanExecutionAction.EndPan:
                EndPhotoPan();
                return true;
            default:
                return true;
        }
    }

    private InputInteractionState CaptureInputInteractionState()
    {
        return InputInteractionStatePolicy.Resolve(
            photoModeActive: _photoModeActive,
            boardActive: IsBoardActive(),
            crossPageDisplayEnabled: IsCrossPageDisplaySettingEnabled());
    }

    private long ResolveStylusBatchSpanTicks(long nowTicks, int sampleCount)
    {
        return StylusSampleTimestampPolicy.ResolveBatchSpanTicks(
            Stopwatch.Frequency,
            nowTicks,
            sampleCount,
            _stylusSampleTimestampState);
    }

    private long EnsureMonotonicStylusTimestamp(long timestampTicks)
    {
        return StylusSampleTimestampPolicy.EnsureMonotonicTimestamp(
            timestampTicks,
            _stylusSampleTimestampState);
    }

    private void RememberStylusSampleTimestamp(long timestampTicks)
    {
        StylusSampleTimestampStateUpdater.Remember(
            ref _stylusSampleTimestampState,
            timestampTicks);
    }

    private void AppendInterpolatedBrushSamples(
        BrushInputSample previous,
        BrushInputSample current,
        ref BrushInputSample? lastChangedSample)
    {
        double distance = (current.Position - previous.Position).Length;
        long totalTicks = Math.Max(
            StylusInterpolationDefaults.MinTimestampStepTicks,
            current.TimestampTicks - previous.TimestampTicks);
        double dtMs = totalTicks * 1000.0 / Math.Max(Stopwatch.Frequency, 1);
        double speedDipPerMs = distance / Math.Max(StylusInterpolationDefaults.MinDtMsForSpeed, dtMs);
        double interpolationStepDip = StylusInterpolationPolicy.ResolveInterpolationStepDip(
            _brushSize,
            distance,
            totalTicks,
            Stopwatch.Frequency);
        if (!StylusInterpolationPolicy.ShouldInterpolate(distance, interpolationStepDip))
        {
            return;
        }

        int maxSegments = StylusInterpolationPolicy.ResolveMaxSegments(speedDipPerMs, dtMs);

        int segmentCount = Math.Clamp(
            (int)Math.Ceiling(distance / interpolationStepDip),
            StylusInterpolationDefaults.MinSegmentCount,
            maxSegments);
        if (segmentCount <= StylusInterpolationDefaults.MinSegmentCount)
        {
            return;
        }

        for (int i = 1; i < segmentCount; i++)
        {
            double t = i / (double)segmentCount;
            if (t >= StylusInterpolationDefaults.SegmentProgressUpperBound)
            {
                break;
            }

            var position = new WpfPoint(
                previous.Position.X + ((current.Position.X - previous.Position.X) * t),
                previous.Position.Y + ((current.Position.Y - previous.Position.Y) * t));
            long timestampTicks = previous.TimestampTicks + Math.Max(
                StylusInterpolationDefaults.MinTimestampStepTicks,
                (long)Math.Round(totalTicks * t));
            timestampTicks = EnsureMonotonicStylusTimestamp(timestampTicks);
            var sample = CreateInterpolatedBrushSample(previous, current, position, timestampTicks, t);
            if (TryUpdateBrushStrokeGeometry(sample))
            {
                lastChangedSample = sample;
            }
            RememberStylusSampleTimestamp(sample.TimestampTicks);
        }
    }

    private static BrushInputSample CreateInterpolatedBrushSample(
        BrushInputSample previous,
        BrushInputSample current,
        WpfPoint position,
        long timestampTicks,
        double t)
    {
        if (!previous.HasPressure || !current.HasPressure)
        {
            return BrushInputSample.CreatePointer(
                position,
                timestampTicks,
                LerpNullable(previous.AzimuthRadians, current.AzimuthRadians, t),
                LerpNullable(previous.AltitudeRadians, current.AltitudeRadians, t),
                LerpNullable(previous.TiltXRadians, current.TiltXRadians, t),
                LerpNullable(previous.TiltYRadians, current.TiltYRadians, t));
        }

        double pressure = previous.Pressure + ((current.Pressure - previous.Pressure) * t);
        return BrushInputSample.CreateStylus(
            position,
            timestampTicks,
            pressure,
            LerpNullable(previous.AzimuthRadians, current.AzimuthRadians, t),
            LerpNullable(previous.AltitudeRadians, current.AltitudeRadians, t),
            LerpNullable(previous.TiltXRadians, current.TiltXRadians, t),
            LerpNullable(previous.TiltYRadians, current.TiltYRadians, t));
    }

    private static double? LerpNullable(double? a, double? b, double t)
    {
        if (!a.HasValue && !b.HasValue)
        {
            return null;
        }

        double from = a ?? b ?? 0.0;
        double to = b ?? a ?? 0.0;
        return from + ((to - from) * t);
    }

    private void CapturePointerInput()
    {
        OverlayRoot.CaptureMouse();
        Stylus.Capture(OverlayRoot);
    }

    private void ReleasePointerInput()
    {
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
        if (OverlayRoot.IsStylusCaptured)
        {
            Stylus.Capture(null);
        }
    }

    private bool IsCrossPageFirstInputTraceActive()
    {
        return false;
    }

    private void BeginCrossPageFirstInputTrace(int fromPage, int toPage)
    {
    }

    private void MarkCrossPageFirstInputStage(string stage, string? details = null)
    {
    }

    private void EndCrossPageFirstInputTrace(string outcome)
    {
    }
}




