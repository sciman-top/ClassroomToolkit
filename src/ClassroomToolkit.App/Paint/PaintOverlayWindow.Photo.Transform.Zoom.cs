using System;
using System.Windows.Media;
using ClassroomToolkit.App.Photos;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private void ZoomPhoto(int delta, WpfPoint center)
    {
        ApplyPhotoZoomInput(PhotoZoomInputSource.Wheel, delta, center);
    }

    private void ZoomPhotoByFactor(double scaleFactor)
    {
        var center = ResolvePhotoZoomAnchor();
        ApplyPhotoZoomInput(PhotoZoomInputSource.Keyboard, scaleFactor, center);
    }

    private void ApplyPhotoZoomInput(PhotoZoomInputSource source, double rawValue, WpfPoint center)
    {
        if (!PhotoZoomNormalizer.TryNormalizeFactor(
                source,
                rawValue,
                _photoWheelZoomBase,
                _photoGestureZoomSensitivity,
                PhotoGestureZoomNoiseThreshold,
                PhotoZoomMinEventFactor,
                PhotoZoomMaxEventFactor,
                out var scaleFactor))
        {
            return;
        }

        MarkPhotoZoomInput();
        MarkPhotoInteractionForRenderQuality();
        LogPhotoInputTelemetry("zoom", $"source={source}; raw={rawValue:0.####}; factor={scaleFactor:0.####}");
        if (source == PhotoZoomInputSource.Wheel)
        {
            QueuePhotoWheelZoom(scaleFactor, center);
            return;
        }

        StopPhotoWheelZoomAnimation(applyTarget: false, scheduleTransformSave: true);
        ApplyPhotoScale(scaleFactor, center);
    }

    public void UpdatePhotoZoomTuning(double wheelBase, double gestureSensitivity)
    {
        wheelBase = double.IsFinite(wheelBase)
            ? wheelBase
            : PhotoZoomInputDefaults.WheelZoomBaseDefault;
        gestureSensitivity = double.IsFinite(gestureSensitivity)
            ? gestureSensitivity
            : PhotoZoomInputDefaults.GestureSensitivityDefault;
        _photoWheelZoomBase = Math.Clamp(
            wheelBase,
            PhotoZoomInputDefaults.WheelZoomBaseMin,
            PhotoZoomInputDefaults.WheelZoomBaseMax);
        _photoGestureZoomSensitivity = Math.Clamp(
            gestureSensitivity,
            PhotoZoomInputDefaults.GestureSensitivityMin,
            PhotoZoomInputDefaults.GestureSensitivityMax);
    }

    public void UpdatePhotoInertiaProfile(string profile)
    {
        _photoInertiaProfile = PhotoInertiaProfileDefaults.Normalize(profile);
        _photoPanInertiaTuning = PhotoPanInertiaProfilePolicy.Resolve(_photoInertiaProfile);
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
    }

    private void ApplyPhotoScale(double scaleFactor, WpfPoint center)
    {
        StopPhotoWheelZoomAnimation(applyTarget: false, scheduleTransformSave: true);
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        EnsurePhotoTransformsWritable();
        var currentScale = _photoScale.ScaleX;
        var newScale = Math.Clamp(
            currentScale * scaleFactor,
            PhotoTransformViewportDefaults.MinScale,
            PhotoTransformViewportDefaults.MaxScale);
        if (Math.Abs(newScale - _photoScale.ScaleX) < PhotoZoomInputDefaults.ScaleApplyEpsilon)
        {
            return;
        }

        var before = ToPhotoSpace(center);
        ApplyPhotoScaleValue(
            newScale,
            center,
            before,
            scheduleTransformSave: true);
    }

    private void QueuePhotoWheelZoom(double scaleFactor, WpfPoint center)
    {
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        EnsurePhotoTransformsWritable();

        var currentScale = _photoScale.ScaleX;
        if (!_photoWheelZoomAnimationActive)
        {
            _photoWheelZoomAnchor = center;
            _photoWheelZoomPhotoPoint = ToPhotoSpace(center);
            _photoWheelZoomTargetScale = currentScale;
            _photoWheelZoomAnimationActive = true;
            _photoWheelZoomLastRenderingTime = TimeSpan.MinValue;
        }

        var targetScale = Math.Clamp(
            _photoWheelZoomTargetScale * scaleFactor,
            PhotoTransformViewportDefaults.MinScale,
            PhotoTransformViewportDefaults.MaxScale);
        _photoWheelZoomTargetScale = targetScale;
        if (Math.Abs(targetScale - currentScale) < PhotoZoomInputDefaults.ScaleApplyEpsilon)
        {
            StopPhotoWheelZoomAnimation(applyTarget: true, scheduleTransformSave: true);
            return;
        }

        EnsurePhotoZoomRenderingAttached();
    }

    private void ApplyPhotoScaleValue(
        double newScale,
        WpfPoint center,
        WpfPoint photoPoint,
        bool scheduleTransformSave)
    {
        if (!double.IsFinite(newScale) || newScale <= 0)
        {
            return;
        }

        if (IsCrossPageDisplayActive())
        {
            // Capture the scale represented by the currently visible neighbor
            // slots before changing the current page.  The next render frame
            // applies only the accumulated ratio, so multiple input events do
            // not repeat the same neighbor traversal.
            SchedulePhotoZoomFrameSync();
        }

        _photoScale.ScaleX = newScale;
        _photoScale.ScaleY = newScale;
        // _photoPageScale normalizes pages in cross-page image sequences.  It is
        // part of the forward transform, so it must also be part of the new
        // translation or the anchor drifts on every zoom step.
        var anchoredTranslation = PhotoInkCoordinateMapper.ResolveZoomAnchoredTranslation(
            center,
            photoPoint,
            _photoPageScale.ScaleX,
            _photoPageScale.ScaleY,
            newScale,
            newScale);
        _photoTranslate.X = anchoredTranslation.X;
        _photoTranslate.Y = anchoredTranslation.Y;

        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        if (scheduleTransformSave)
        {
            SchedulePhotoTransformSave(userAdjusted: true);
        }

        SyncPhotoInteractiveRefreshAnchor();
        RequestPhotoTransformInkRedraw();
    }

    private void SchedulePhotoZoomFrameSync()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }

        if (!_photoZoomFramePending)
        {
            _photoZoomLastNeighborLayoutScale = _photoScale.ScaleX;
        }

        _photoZoomFramePending = true;
        EnsurePhotoZoomRenderingAttached();
    }

    private void EnsurePhotoZoomRenderingAttached()
    {
        if (_photoZoomRenderingAttached)
        {
            return;
        }

        CompositionTarget.Rendering += OnPhotoZoomRendering;
        _photoZoomRenderingAttached = true;
    }

    private void OnPhotoZoomRendering(object? sender, EventArgs e)
    {
        AdvancePhotoWheelZoom(e as RenderingEventArgs);
        if (_photoZoomFramePending)
        {
            FlushPhotoZoomFrameSync();
        }

        if (!_photoWheelZoomAnimationActive && !_photoZoomFramePending)
        {
            DetachPhotoZoomRendering();
        }
    }

    private void AdvancePhotoWheelZoom(RenderingEventArgs? renderingArgs)
    {
        if (!_photoWheelZoomAnimationActive)
        {
            return;
        }

        if (!_photoModeActive || _photoPanning || _photoManipulating || IsInkOperationActive())
        {
            StopPhotoWheelZoomAnimation(applyTarget: false, scheduleTransformSave: true);
            return;
        }

        var elapsedMs = 16.0;
        if (renderingArgs != null)
        {
            if (_photoWheelZoomLastRenderingTime != TimeSpan.MinValue
                && renderingArgs.RenderingTime > _photoWheelZoomLastRenderingTime)
            {
                elapsedMs = (renderingArgs.RenderingTime - _photoWheelZoomLastRenderingTime).TotalMilliseconds;
            }
            _photoWheelZoomLastRenderingTime = renderingArgs.RenderingTime;
        }
        elapsedMs = Math.Clamp(elapsedMs, 1.0, 50.0);

        var currentScale = _photoScale.ScaleX;
        var targetScale = _photoWheelZoomTargetScale;
        var remaining = targetScale - currentScale;
        if (Math.Abs(remaining) <= PhotoTransformTimingDefaults.SmoothZoomFrameEpsilon)
        {
            if (Math.Abs(remaining) > PhotoZoomInputDefaults.ScaleApplyEpsilon)
            {
                ApplyPhotoScaleValue(
                    targetScale,
                    _photoWheelZoomAnchor,
                    _photoWheelZoomPhotoPoint,
                    scheduleTransformSave: false);
            }
            CompletePhotoWheelZoomAnimation(scheduleTransformSave: true);
            return;
        }

        var response = Math.Max(1.0, PhotoTransformTimingDefaults.SmoothZoomResponseMs);
        var interpolation = 1.0 - Math.Exp(-elapsedMs / response);
        var nextScale = currentScale + (remaining * interpolation);
        if (Math.Abs(targetScale - nextScale) <= PhotoTransformTimingDefaults.SmoothZoomFrameEpsilon)
        {
            nextScale = targetScale;
        }

        ApplyPhotoScaleValue(
            nextScale,
            _photoWheelZoomAnchor,
            _photoWheelZoomPhotoPoint,
            scheduleTransformSave: false);
        if (Math.Abs(targetScale - nextScale) <= PhotoTransformTimingDefaults.SmoothZoomFrameEpsilon)
        {
            CompletePhotoWheelZoomAnimation(scheduleTransformSave: true);
        }
    }

    private void FlushPhotoZoomFrameSync()
    {
        _photoZoomFramePending = false;
        if (!IsCrossPageDisplayActive())
        {
            _photoZoomLastNeighborLayoutScale = double.NaN;
            return;
        }

        var currentScale = _photoScale.ScaleX;
        var previousScale = _photoZoomLastNeighborLayoutScale;
        if (!double.IsFinite(previousScale) || previousScale <= 0)
        {
            previousScale = currentScale;
        }

        var layoutScaleFactor = previousScale > 0
            ? currentScale / previousScale
            : 1.0;
        if (CrossPageZoomLayoutScalePolicy.ShouldSynchronize(layoutScaleFactor))
        {
            SyncNeighborLayoutForZoom(layoutScaleFactor);
        }
        _photoZoomLastNeighborLayoutScale = currentScale;

        // Transform-only updates keep existing frames coherent at the current
        // scale.  The normal cross-page dispatcher then performs at most one
        // latest-state visibility/frame refresh for this compositor frame.
        UpdateNeighborTransformsForPan(includeScale: true);
        RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ApplyScale);
    }

    private void CompletePhotoWheelZoomAnimation(bool scheduleTransformSave)
    {
        var wasActive = _photoWheelZoomAnimationActive;
        _photoWheelZoomAnimationActive = false;
        _photoWheelZoomLastRenderingTime = TimeSpan.MinValue;
        if (wasActive && scheduleTransformSave)
        {
            SchedulePhotoTransformSave(userAdjusted: true);
        }
    }

    private void StopPhotoWheelZoomAnimation(bool applyTarget, bool scheduleTransformSave)
    {
        if (!_photoWheelZoomAnimationActive)
        {
            return;
        }

        var targetScale = _photoWheelZoomTargetScale;
        var anchor = _photoWheelZoomAnchor;
        var photoPoint = _photoWheelZoomPhotoPoint;
        var shouldSave = scheduleTransformSave;
        CompletePhotoWheelZoomAnimation(scheduleTransformSave: false);
        if (applyTarget
            && double.IsFinite(targetScale)
            && targetScale > 0
            && Math.Abs(targetScale - _photoScale.ScaleX) > PhotoZoomInputDefaults.ScaleApplyEpsilon)
        {
            ApplyPhotoScaleValue(
                targetScale,
                anchor,
                photoPoint,
                scheduleTransformSave: false);
        }
        if (shouldSave)
        {
            SchedulePhotoTransformSave(userAdjusted: true);
        }

        if (!_photoZoomFramePending)
        {
            DetachPhotoZoomRendering();
        }
    }

    private void DetachPhotoZoomRendering()
    {
        if (!_photoZoomRenderingAttached)
        {
            return;
        }

        CompositionTarget.Rendering -= OnPhotoZoomRendering;
        _photoZoomRenderingAttached = false;
    }

    private void StopPhotoZoomRendering()
    {
        // Preserve the latest interpolated scale when the mode/window is torn
        // down before the wheel animation reaches its target.
        StopPhotoWheelZoomAnimation(applyTarget: false, scheduleTransformSave: true);
        _photoZoomFramePending = false;
        _photoZoomLastNeighborLayoutScale = double.NaN;
        DetachPhotoZoomRendering();
    }

    private void MarkPhotoZoomNeighborLayoutSynchronized()
    {
        _photoZoomLastNeighborLayoutScale = IsCrossPageDisplayActive()
            ? _photoScale.ScaleX
            : double.NaN;
    }
}
