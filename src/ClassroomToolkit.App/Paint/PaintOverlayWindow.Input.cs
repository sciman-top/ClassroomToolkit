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

    private bool ResumeCrossPageInputOperationAfterSwitch(bool switchedPage, BrushInputSample input)
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
            // SaveCurrentPageOnNavigate(finalizeActiveOperation=true) releases pointer capture;
            // reacquire here so subsequent move/up events remain reliable during seam-cross writing.
            CapturePointerInput();
            _visualHost.Clear();
            BeginBrushStrokeContinuation(seed, renderInitialPreview: false);
            if (!executionPlan.ShouldUpdateBrushAfterContinuation)
            {
                return false;
            }

            // Replay the current sample immediately after seam continuation so the
            // first segment on the new page stays aligned with stylus motion.
            BrushInputSample? lastChangedSample = null;
            AppendCrossPageContinuationSamples(seed, input, ref lastChangedSample);
            if (TryUpdateBrushStrokeGeometry(input))
            {
                lastChangedSample = input;
            }
            if (lastChangedSample.HasValue)
            {
                FlushBrushStrokePreview(lastChangedSample.Value);
            }
            return true;
        }

        if (executionPlan.Action == CrossPageInputResumeAction.BeginEraser)
        {
            BeginEraser(input.Position);
        }

        return false;
    }

    private void AppendCrossPageContinuationSamples(
        BrushInputSample previous,
        BrushInputSample current,
        ref BrushInputSample? lastChangedSample)
    {
        var distance = (current.Position - previous.Position).Length;
        if (distance <= 0.5)
        {
            return;
        }

        long totalTicks = Math.Max(1, current.TimestampTicks - previous.TimestampTicks);
        var segmentCount = Math.Clamp(
            (int)Math.Ceiling(distance / 0.9),
            2,
            64);
        if (segmentCount <= 2)
        {
            return;
        }

        for (int i = 1; i < segmentCount; i++)
        {
            var t = i / (double)segmentCount;
            if (t >= 1.0)
            {
                break;
            }

            var position = new WpfPoint(
                previous.Position.X + ((current.Position.X - previous.Position.X) * t),
                previous.Position.Y + ((current.Position.Y - previous.Position.Y) * t));
            var timestampTicks = previous.TimestampTicks + Math.Max(
                1,
                (long)Math.Round(totalTicks * t));
            var sample = CreateInterpolatedBrushSample(previous, current, position, timestampTicks, t);
            if (TryUpdateBrushStrokeGeometry(sample))
            {
                lastChangedSample = sample;
            }
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


