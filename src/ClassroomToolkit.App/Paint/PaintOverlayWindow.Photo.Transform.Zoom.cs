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
        var center = new WpfPoint(OverlayRoot.ActualWidth / 2.0, OverlayRoot.ActualHeight / 2.0);
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
        ApplyPhotoScale(scaleFactor, center);
    }

    public void UpdatePhotoZoomTuning(double wheelBase, double gestureSensitivity)
    {
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
        _photoScale.ScaleX = newScale;
        _photoScale.ScaleY = newScale;
        _photoTranslate.X = center.X - before.X * newScale;
        _photoTranslate.Y = center.Y - before.Y * newScale;
        if (IsCrossPageDisplayActive())
        {
            var layoutScaleFactor = currentScale > 0 ? newScale / currentScale : 1.0;
            SyncNeighborLayoutForZoom(layoutScaleFactor);
            if (!IsPhotoZoomInteractionActive())
            {
                ApplyCrossPageBoundaryLimits();
            }
            // Keep visible neighbor pages visually locked to the current page during zoom.
            UpdateNeighborTransformsForPan(includeScale: true);
            // Recompute neighbor visibility/layout during zoom so seam pages don't lag until
            // post-interaction refresh.
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ApplyScale);
        }

        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SchedulePhotoTransformSave(userAdjusted: true);

        SyncPhotoInteractiveRefreshAnchor();
        RequestPhotoTransformInkRedraw();
    }
}
