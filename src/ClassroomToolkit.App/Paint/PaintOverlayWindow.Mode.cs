using System.Diagnostics;
using System.Windows.Threading;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    public void SetMode(PaintToolMode mode)
    {
        if (mode != _mode)
        {
            var reason = $"mode-switch:{_mode}->{mode}";
            StopActivePhotoManipulation(reason);
            if (_strokeInProgress)
            {
                ResetInterruptedBrushState();
            }
            if (_isErasing)
            {
                var position = _lastEraserPoint ?? _lastPointerPosition;
                if (position.HasValue)
                {
                    EndEraser(position.Value);
                }
                else
                {
                    _isErasing = false;
                    _lastEraserPoint = null;
                    _lastEraserAppliedPoint = null;
                    _activeInkOperationHistory = null;
                }
            }
            if (HasPendingShapeDraft())
            {
                CancelPendingShapeDraft(reason);
            }
            if (_isRegionSelecting)
            {
                ClearRegionSelection();
                DiscardActiveInkOperationHistory();
            }
            ReleasePointerInput();
        }

        _mode = mode;
        DispatchSessionEvent(new SwitchToolModeEvent(MapSessionToolMode(mode)));
        UpdateOverlayHitTestVisibility();
        if (PhotoCursorModeFocusRequestPolicy.ShouldRequestFocus(_photoModeActive, mode))
        {
            SafeActionExecutionExecutor.TryExecute(
                () => PhotoCursorModeFocusRequested?.Invoke(),
                ex => Debug.WriteLine($"[PhotoCursorModeFocusRequested] callback failed: {ex.GetType().Name} - {ex.Message}"));
        }

        var isPaintMode = mode != PaintToolMode.Cursor;
        PaintModeManager.Instance.IsPaintMode = isPaintMode;

        if (mode == PaintToolMode.Cursor)
        {
            Cursor = System.Windows.Input.Cursors.Arrow;
        }
        else
        {
            var cursorUpdateScheduled = TryBeginInvoke(() =>
            {
                UpdateCursor(mode);
            }, DispatcherPriority.Normal);
            if (!cursorUpdateScheduled && Dispatcher.CheckAccess())
            {
                UpdateCursor(mode);
            }
        }

        if (mode != PaintToolMode.RegionErase)
        {
            ClearRegionSelection();
        }
        if (mode != PaintToolMode.Shape)
        {
            ClearShapePreview();
        }
        HideEraserPreview();

        UpdateInputPassthrough();
        if (PhotoPanModeSwitchPolicy.ShouldEndPan(
                _photoPanning,
                _photoModeActive,
                IsBoardActive(),
                _mode,
                IsInkOperationActive()))
        {
            EndPhotoPan(allowInertia: false);
        }

        var modeFollowUpScheduled = TryBeginInvoke(() =>
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            if (mode == PaintToolMode.Cursor)
            {
                RestorePresentationFocusIfNeeded(requireFullscreen: false);
            }
        }, DispatcherPriority.Background);
        if (!modeFollowUpScheduled && Dispatcher.CheckAccess())
        {
            UpdateWpsNavHookState();
            UpdateFocusAcceptance();
            if (mode == PaintToolMode.Cursor)
            {
                RestorePresentationFocusIfNeeded(requireFullscreen: false);
            }
        }
    }
}
