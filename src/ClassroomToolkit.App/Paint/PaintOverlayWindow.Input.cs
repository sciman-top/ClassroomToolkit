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

    private void CapturePointerInput()
    {
        OverlayRoot.CaptureMouse();
        Stylus.Capture(OverlayRoot);
        PaintModeManager.Instance.IsDrawing = true;
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
        PaintModeManager.Instance.IsDrawing = false;
    }

}


