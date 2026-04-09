using System.Windows.Input;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
        if (e.RightButton == System.Windows.Input.MouseButtonState.Pressed || _photoRightClickPending || _photoPanning)
        {
            UpdatePhotoRightClickPendingByMove(position);
        }
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
        PaintModeManager.Instance.IsDrawing = false;
        var interactionState = CaptureInputInteractionState();
        var lostCapturePlan = OverlayLostMouseCaptureExecutionPolicy.Resolve(
            IsMousePhotoPanActive(interactionState),
            rightClickPending: _photoRightClickPending);
        if (lostCapturePlan.ShouldEndPan)
        {
            EndPhotoPan(allowInertia: false);
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
            EndPhotoPan(allowInertia: false);
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
            UpdatePhotoPanVelocitySamples(position);
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

        UpdatePhotoPanVelocitySamples(e.GetPosition(OverlayRoot));
        EndPhotoPan();
        e.Handled = executionPlan.ShouldMarkHandled;
        return true;
    }
}