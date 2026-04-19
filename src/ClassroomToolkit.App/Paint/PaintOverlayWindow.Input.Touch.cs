using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnTouchDown(object? sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Add(e.TouchDevice.Id);
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);

        if (_photoTouchPanDeviceId.HasValue && _photoTouchPanDeviceId != e.TouchDevice.Id)
        {
            EndPhotoPan(allowInertia: false);
        }

        if (!PhotoTouchInteractionPolicy.ShouldUseSingleTouchPan(
                _photoModeActive,
                IsBoardActive(),
                _mode,
                IsInkOperationActive(),
                _photoActiveTouchIds.Count))
        {
            return;
        }

        _photoTouchPanDeviceId = e.TouchDevice.Id;
        OverlayRoot.CaptureTouch(e.TouchDevice);
        BeginPhotoPan(
            e.GetTouchPoint(OverlayRoot).Position,
            PhotoPanPointerKind.Touch,
            captureStylus: false);
        MarkPhotoGestureInput();
        e.Handled = true;
    }

    private void OnTouchMove(object? sender, TouchEventArgs e)
    {
        if (_photoTouchPanDeviceId != e.TouchDevice.Id || _photoActiveTouchIds.Count != 1)
        {
            return;
        }

        UpdatePhotoPan(e.GetTouchPoint(OverlayRoot).Position);
        MarkPhotoGestureInput();
        e.Handled = true;
    }

    private void OnTouchUp(object? sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Remove(e.TouchDevice.Id);
        if (_photoTouchPanDeviceId != e.TouchDevice.Id)
        {
            return;
        }

        UpdatePhotoPanVelocitySamples(e.GetTouchPoint(OverlayRoot).Position);
        OverlayRoot.ReleaseTouchCapture(e.TouchDevice);
        EndPhotoPan();
        MarkPhotoGestureInput();
        e.Handled = true;
    }

    private void OnOverlayLostTouchCapture(object? sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Remove(e.TouchDevice.Id);
        if (_photoTouchPanDeviceId != e.TouchDevice.Id)
        {
            return;
        }

        EndPhotoPan(allowInertia: false);
        e.Handled = true;
    }
}
