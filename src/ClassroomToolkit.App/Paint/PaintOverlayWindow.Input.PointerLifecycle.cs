using System;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Paint.Brushes;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
        var consumedByCrossPageResume = ResumeCrossPageInputOperationAfterSwitch(switchedPage, input);
        if (consumedByCrossPageResume)
        {
            return;
        }
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
        if (_photoUnboundedInkCanvasEnabled)
        {
            return false;
        }

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
            photoFullscreenActive: IsPhotoFullscreenActive,
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
}