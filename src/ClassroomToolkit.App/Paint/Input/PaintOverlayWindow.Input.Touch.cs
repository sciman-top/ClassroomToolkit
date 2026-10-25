using System.Windows.Input;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnTouchDown(object? sender, TouchEventArgs e)
    {
        if (!ShouldContinuePointerInput(e))
        {
            return;
        }

        _photoActiveTouchIds.Add(e.TouchDevice.Id);
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        // Do not capture or mark TouchDown handled here.  WPF uses an
        // unhandled TouchDown to capture the first manipulator and create the
        // ManipulationStarting/Delta stream.  The manipulation path handles
        // both one-finger pan and two-finger pinch/translate.
    }

    private void OnTouchMove(object? sender, TouchEventArgs e)
    {
        // ManipulationDelta owns photo movement.  Leaving TouchMove
        // unhandled keeps WPF's manipulator capture intact.
    }

    private void OnTouchUp(object? sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Remove(e.TouchDevice.Id);
    }

    private void OnOverlayLostTouchCapture(object? sender, TouchEventArgs e)
    {
        _photoActiveTouchIds.Remove(e.TouchDevice.Id);
    }
}
