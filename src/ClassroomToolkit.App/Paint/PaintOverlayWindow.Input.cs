using System;
using System.Windows;
using System.Windows.Input;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void OnMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
    {
        if (IsBoardActive())
        {
            e.Handled = true;
            return;
        }
        if (_photoModeActive)
        {
            ZoomPhoto(e.Delta, e.GetPosition(OverlayRoot));
            e.Handled = true;
            return;
        }
        if (_mode != PaintToolMode.Cursor
            && _mode != PaintToolMode.Brush
            && _mode != PaintToolMode.Shape
            && _mode != PaintToolMode.Eraser
            && _mode != PaintToolMode.RegionErase)
        {
            return;
        }
        if (_mode == PaintToolMode.Cursor && _inputPassthroughEnabled)
        {
            return;
        }
        if (!_presentationOptions.AllowOffice && !_presentationOptions.AllowWps)
        {
            return;
        }
        if (_wpsNavHookActive && _wpsHookInterceptWheel)
        {
            var foregroundType = ResolveForegroundPresentationType();
            if (foregroundType == ClassroomToolkit.Interop.Presentation.PresentationType.Wps)
            {
                return;
            }
        }
        if (WpsHookRecentlyFired())
        {
            return;
        }
        var command = e.Delta < 0
            ? ClassroomToolkit.Services.Presentation.PresentationCommand.Next
            : ClassroomToolkit.Services.Presentation.PresentationCommand.Previous;
        if (TrySendPresentationCommand(command))
        {
            e.Handled = true;
        }
    }

    private void OnKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (_photoLoading)
        {
            e.Handled = true;
            return;
        }
        if (TryHandlePhotoKey(e.Key))
        {
            e.Handled = true;
            return;
        }
        if (IsBoardActive() || _photoModeActive)
        {
            return;
        }
        if (TryHandlePresentationKey(e.Key))
        {
            e.Handled = true;
        }
    }

    public bool TryHandlePhotoKey(Key key)
    {
        if (!_photoModeActive || IsBoardActive())
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
            if (TryNavigatePdf(direction))
            {
                return true;
            }
            if (!IsAtFileSequenceBoundary(direction))
            {
                PhotoNavigationRequested?.Invoke(direction);
            }
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

}
