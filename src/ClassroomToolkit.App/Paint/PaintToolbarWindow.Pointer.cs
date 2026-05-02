using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using ClassroomToolkit.App.Windowing;

namespace ClassroomToolkit.App.Paint;

public partial class PaintToolbarWindow
{
    private void OnRegionCaptureResumeTimerTick(object? sender, EventArgs e)
    {
        if (!_resumeRegionCaptureArmed)
        {
            _regionCaptureResumeTimer.Stop();
            return;
        }

        if (!IsVisible || !IsLoaded)
        {
            return;
        }

        if (BoardActive || _overlay?.IsWhiteboardActive == true)
        {
            ClearDirectWhiteboardEntryArm();
            return;
        }

        TryResumeRegionCaptureIfPointerOutsideToolbar();
    }

    private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        RecordInteractionScreenPoint(e.GetPosition(this));

        var button = FindButtonBase(e.OriginalSource as DependencyObject);
        if (button == null)
        {
            return;
        }

        var captureInteractionActive = _resumeRegionCaptureArmed || _directWhiteboardEntryArmed || _regionCapturePending;
        if (!captureInteractionActive)
        {
            return;
        }

        if (ReferenceEquals(button, BoardButton))
        {
            RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress();
            return;
        }

        if (!ToolbarResumeCancellationPolicy.ShouldCancelPendingResumeOnToolbarPress(
                captureInteractionActive,
                pressedToolbarButton: true,
                pressedBoardButton: ReferenceEquals(button, BoardButton)))
        {
            return;
        }

        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarHandledPress();
        ClearDirectWhiteboardEntryArm();
    }

    private void OnPreviewTouchDown(object? sender, TouchEventArgs e)
    {
        RecordInteractionScreenPoint(e.GetTouchPoint(this).Position);
    }

    private void OnToolbarMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove();
    }

    private void OnToolbarMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        TryResumeRegionCaptureIfPointerOutsideToolbar();
    }

    private static System.Windows.Controls.Primitives.ButtonBase? FindButtonBase(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase button)
            {
                return button;
            }

            current = GetParent(current);
        }

        return null;
    }

    private void OnToolbarDragStart(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _toolbarDragging = true;
        _toolbarDragOffset = e.GetPosition(this);
        _toolbarDragScope?.Dispose();
        _toolbarDragScope = WindowDragOperationState.Begin();
        CaptureMouse();
        e.Handled = true;
    }

    private void OnToolbarTouchDragStart(object sender, TouchEventArgs e)
    {
        if (_toolbarTouchDragging)
        {
            return;
        }

        if (IsInteractiveElement(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _toolbarTouchDragging = true;
        _activeToolbarTouchDevice = e.TouchDevice;
        _toolbarTouchDragOffset = e.GetTouchPoint(this).Position;
        _toolbarDragScope?.Dispose();
        _toolbarDragScope = WindowDragOperationState.Begin();
        CaptureTouch(_activeToolbarTouchDevice);
        e.Handled = true;
    }

    private void OnToolbarDragMove(object? sender, System.Windows.Input.MouseEventArgs e)
    {
        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove();

        if (!_toolbarDragging)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndToolbarDragCore();
            return;
        }

        var screen = PointToScreen(e.GetPosition(this));
        MoveToolbarWithinVirtualScreen(screen.X - _toolbarDragOffset.X, screen.Y - _toolbarDragOffset.Y);
    }

    private void TryResumeRegionCaptureIfPointerOutsideToolbar()
    {
        var screenPoint = System.Windows.Forms.Cursor.Position;
        var decision = RegionCaptureResumeTriggerPolicy.Resolve(
            _resumeRegionCaptureArmed,
            IsVisible,
            IsLoaded,
            BoardActive,
            _overlay?.IsWhiteboardActive == true,
            IsPointInsideToolbar(screenPoint.X, screenPoint.Y));
        if (decision.ShouldClearDirectWhiteboardEntryArm)
        {
            ClearDirectWhiteboardEntryArm();
            return;
        }

        if (!decision.ShouldResumeRegionCapture)
        {
            return;
        }

        _resumeRegionCaptureArmed = false;
        SafeActionExecutionExecutor.TryExecute(
            () => RegionCaptureRequested?.Invoke(),
            ex => System.Diagnostics.Debug.WriteLine($"PaintToolbar: region capture resume callback failed: {ex.Message}"));
    }

    private void OnToolbarDragEnd(object? sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        EndToolbarDragCore();
    }

    private void EndToolbarDragCore()
    {
        if (!_toolbarDragging)
        {
            return;
        }

        _toolbarDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        _toolbarDragScope?.Dispose();
        _toolbarDragScope = null;
    }

    private void OnToolbarTouchDragMove(object? sender, TouchEventArgs e)
    {
        RegionScreenCaptureWorkflow.CancelActiveSelectionFromToolbarPointerMove();

        if (!_toolbarTouchDragging || !ReferenceEquals(_activeToolbarTouchDevice, e.TouchDevice))
        {
            return;
        }

        var screen = PointToScreen(e.GetTouchPoint(this).Position);
        MoveToolbarWithinVirtualScreen(screen.X - _toolbarTouchDragOffset.X, screen.Y - _toolbarTouchDragOffset.Y);
        e.Handled = true;
    }

    private void OnToolbarTouchDragEnd(object? sender, TouchEventArgs e)
    {
        if (!_toolbarTouchDragging || !ReferenceEquals(_activeToolbarTouchDevice, e.TouchDevice))
        {
            return;
        }

        EndToolbarTouchDragCore();
        e.Handled = true;
    }

    private void OnToolbarTouchLostCapture(object? sender, TouchEventArgs e)
    {
        if (_toolbarTouchDragging && ReferenceEquals(_activeToolbarTouchDevice, e.TouchDevice))
        {
            EndToolbarTouchDragCore();
        }
    }

    private void EndToolbarTouchDragCore()
    {
        if (!_toolbarTouchDragging)
        {
            return;
        }

        _toolbarTouchDragging = false;
        if (_activeToolbarTouchDevice != null)
        {
            ReleaseTouchCapture(_activeToolbarTouchDevice);
            _activeToolbarTouchDevice = null;
        }

        _toolbarDragScope?.Dispose();
        _toolbarDragScope = null;
    }

    private void MoveToolbarWithinVirtualScreen(double proposedLeft, double proposedTop)
    {
        var clampedLeft = Math.Max(
            SystemParameters.VirtualScreenLeft,
            Math.Min(proposedLeft, SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - Width));
        var clampedTop = Math.Max(
            SystemParameters.VirtualScreenTop,
            Math.Min(proposedTop, SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - Height));

        Left = clampedLeft;
        Top = clampedTop;
    }

    private static bool IsInteractiveElement(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is System.Windows.Controls.Primitives.ButtonBase)
            {
                return true;
            }

            current = GetParent(current);
        }

        return false;
    }

    private static DependencyObject? GetParent(DependencyObject obj)
    {
        if (obj is System.Windows.Documents.TextElement textElement)
        {
            return textElement.Parent;
        }

        if (obj is FrameworkContentElement contentElement)
        {
            return contentElement.Parent;
        }

        var parent = VisualTreeHelper.GetParent(obj);
        if (parent == null && obj is FrameworkElement element)
        {
            parent = element.Parent as DependencyObject;
        }

        return parent ?? LogicalTreeHelper.GetParent(obj);
    }

    private void RecordInteractionScreenPoint(System.Windows.Point point)
    {
        var screenPoint = PointToScreen(point);
        _lastInteractionScreenPoint = new System.Drawing.Point(
            (int)Math.Round(screenPoint.X),
            (int)Math.Round(screenPoint.Y));
    }

    private void OnPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        var overlay = _overlay;
        if (overlay == null)
        {
            return;
        }

        e.Handled = AuxWindowKeyRoutingHandler.TryHandle(
            e.Key,
            overlayVisible: overlay.IsVisible,
            tryHandlePhotoKey: overlay.TryHandlePhotoKey,
            canRoutePresentationInput: overlay.CanRoutePresentationInputFromAuxWindow(),
            tryForwardPresentationKey: overlay.ForwardKeyboardToPresentation);
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var overlay = _overlay;
        if (overlay == null)
        {
            return;
        }

        var handled = AuxWindowWheelRoutingHandler.TryHandle(
            delta: e.Delta,
            overlayVisible: overlay.IsVisible,
            canRoutePresentationInput: overlay.CanRoutePresentationInputFromAuxWindow(),
            tryForwardPresentationWheel: overlay.ForwardWheelToPresentation);
        if (handled)
        {
            e.Handled = true;
        }
    }
}
