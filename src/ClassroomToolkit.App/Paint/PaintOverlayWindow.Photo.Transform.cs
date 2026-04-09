using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint.Brushes;
using ClassroomToolkit.App.Windowing;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private bool TryStepPhotoViewport(int direction)
    {
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        var viewportHeight = ResolvePhotoViewportHeight();
        if (viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return false;
        }
        EnsurePhotoTransformsWritable();
        var step = PhotoViewportStepPolicy.ResolveStep(viewportHeight);
        var originalY = _photoTranslate.Y;
        _photoTranslate.Y -= direction * step;

        if (IsCrossPageDisplayActive())
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            SyncCurrentPageToViewportCenter();
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.StepViewport);
        }
        else
        {
            ClampSinglePageTranslateY(viewportHeight);
        }

        var moved = Math.Abs(_photoTranslate.Y - originalY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        if (!moved)
        {
            return false;
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        UpdatePhotoInkClip();
        RequestPhotoTransformInkRedraw();
        return true;
    }

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
        double newScale = Math.Clamp(
            _photoScale.ScaleX * scaleFactor,
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
            ApplyCrossPageBoundaryLimits();
        }
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SchedulePhotoTransformSave(userAdjusted: true);
        if (IsCrossPageDisplayActive())
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.ApplyScale);
        }
        SyncPhotoInteractiveRefreshAnchor();
        RequestPhotoTransformInkRedraw();
    }

    private void RequestPhotoTransformInkRedraw()
    {
        if (!IsPhotoInkModeActive())
        {
            return;
        }

        if (TryEnforceRuntimeEmptyGuardForCurrentPage())
        {
            return;
        }

        MarkInkTransformVersionDirty();
        RequestInkRedraw();
    }

    private void SyncPhotoInteractiveRefreshAnchor()
    {
        _lastPhotoInteractiveRefreshTranslateX = _photoTranslate.X;
        _lastPhotoInteractiveRefreshTranslateY = _photoTranslate.Y;
    }

    private void UpdatePhotoInkPanCompensation()
    {
        var delta = PhotoInkPanCompensationPolicy.Resolve(
            IsPhotoInkModeActive(),
            _photoTranslate.X,
            _photoTranslate.Y,
            _lastInkRedrawPhotoTranslateX,
            _lastInkRedrawPhotoTranslateY);
        _photoInkPanCompensation.X = delta.X;
        _photoInkPanCompensation.Y = delta.Y;
        UpdatePhotoInkClip();
    }

    private void ResetPhotoInkPanCompensation(bool syncToCurrentPhotoTranslate)
    {
        _photoInkPanCompensation.X = 0;
        _photoInkPanCompensation.Y = 0;
        if (syncToCurrentPhotoTranslate)
        {
            _lastInkRedrawPhotoTranslateX = _photoTranslate.X;
            _lastInkRedrawPhotoTranslateY = _photoTranslate.Y;
            SyncPhotoInteractiveRefreshAnchor();
        }
        UpdatePhotoInkClip();
    }

    private WpfPoint ToPhotoSpace(WpfPoint point)
    {
        if (!PhotoInteractionModePolicy.IsPhotoTransformEnabled(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive()))
        {
            return point;
        }

        var inverse = GetPhotoInverseMatrix();
        return inverse.Transform(point);
    }

    private Geometry? ToPhotoGeometry(Geometry geometry)
    {
        if (!PhotoInteractionModePolicy.IsPhotoTransformEnabled(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive())
            || geometry == null)
        {
            return geometry;
        }

        var inverse = GetPhotoInverseMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(inverse);
        var flattened = clone.GetFlattenedPathGeometry();
        if (flattened.CanFreeze)
        {
            flattened.Freeze();
        }

        return flattened;
    }

    private Geometry? ToScreenGeometry(Geometry geometry)
    {
        if (!PhotoInteractionModePolicy.IsPhotoTransformEnabled(
                photoModeActive: _photoModeActive,
                boardActive: IsBoardActive())
            || geometry == null)
        {
            return geometry;
        }
        return PhotoInkCoordinateMapper.ToScreenGeometry(
            geometry,
            _photoPageScale.ScaleX,
            _photoPageScale.ScaleY,
            _photoScale.ScaleX,
            _photoScale.ScaleY,
            _photoTranslate.X,
            _photoTranslate.Y);
    }

    private Matrix GetPhotoMatrix()
    {
        return PhotoInkCoordinateMapper.CreateForwardMatrix(
            _photoPageScale.ScaleX,
            _photoPageScale.ScaleY,
            _photoScale.ScaleX,
            _photoScale.ScaleY,
            _photoTranslate.X,
            _photoTranslate.Y);
    }

    private Matrix GetPhotoInverseMatrix()
    {
        if (PhotoInkCoordinateMapper.TryCreateInverseMatrix(
                _photoPageScale.ScaleX,
                _photoPageScale.ScaleY,
                _photoScale.ScaleX,
                _photoScale.ScaleY,
                _photoTranslate.X,
                _photoTranslate.Y,
                out var inverse,
                PhotoTransformMathDefaults.InverseScaleEpsilon))
        {
            _lastValidPhotoInverseMatrix = inverse;
            return inverse;
        }

        return _lastValidPhotoInverseMatrix;
    }

    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        EnsureRasterSurface();
        if (!_photoModeActive || _photoUserTransformDirty)
        {
            return;
        }
        if (PhotoBackground.Source is BitmapSource bitmap)
        {
            ApplyPhotoFitToViewport(bitmap);
        }
    }

    private void OnWindowStateChanged(object? sender, EventArgs e)
    {
        if (!_photoModeActive)
        {
            return;
        }
        if (WindowState == WindowState.Minimized)
        {
            _photoRestoreFullscreenPending = PhotoWindowStateRestorePolicy.ShouldArmFullscreenRestore(_photoFullscreen);
            // Save current zoom/pan state before minimizing
            SavePhotoTransformState(true);
            return;
        }
        if (PhotoWindowStateRestorePolicy.ShouldRestoreFullscreen(_photoRestoreFullscreenPending, WindowState))
        {
            _photoRestoreFullscreenPending = false;
            _photoFullscreen = true;
            SetPhotoWindowMode(fullscreen: true);

            // Restore PDF page rendering
            if (_photoDocumentIsPdf && _pdfDocument != null)
            {
                RenderPdfPage(_currentPageIndex);
            }

            // Restore zoom/pan state if remember transform is enabled
            if (_rememberPhotoTransform)
            {
                var key = GetCurrentPhotoTransformKey();
                if (!IsCrossPageDisplayActive() && TryApplyStoredPhotoTransform(key))
                {
                }
                else
                {
                    ApplyLastUnifiedPhotoTransform(markUserDirty: false);
                }
                ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
                RequestPhotoTransformInkRedraw();
            }
        }
    }

    private readonly struct PhotoTransformState
    {
        public PhotoTransformState(double scaleX, double scaleY, double translateX, double translateY, bool userAdjusted)
        {
            ScaleX = scaleX;
            ScaleY = scaleY;
            TranslateX = translateX;
            TranslateY = translateY;
            UserAdjusted = userAdjusted;
        }

        public double ScaleX { get; }
        public double ScaleY { get; }
        public double TranslateX { get; }
        public double TranslateY { get; }
        public bool UserAdjusted { get; }
    }

    private string GetCurrentPhotoTransformKey()
    {
        if (string.IsNullOrWhiteSpace(_currentDocumentPath))
        {
            return string.Empty;
        }
        return BuildPhotoModeCacheKey(_currentDocumentPath, _currentPageIndex, _photoDocumentIsPdf);
    }

    private bool TryApplyStoredPhotoTransform(string cacheKey)
    {
        if (!_rememberPhotoTransform)
        {
            _photoUserTransformDirty = false;
            return false;
        }
        if (IsCrossPageDisplayActive())
        {
            return false;
        }
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        if (!_photoPageTransforms.TryGetValue(cacheKey, out var state))
        {
            _photoUserTransformDirty = false;
            return false;
        }
        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = state.ScaleX;
        _photoScale.ScaleY = state.ScaleY;
        _photoTranslate.X = state.TranslateX;
        _photoTranslate.Y = state.TranslateY;
        _photoUserTransformDirty = state.UserAdjusted;
        return true;
    }

    private void SavePhotoTransformState(bool userAdjusted)
    {
        _lastPhotoScaleX = _photoScale.ScaleX;
        _lastPhotoScaleY = _photoScale.ScaleY;
        _lastPhotoTranslateX = _photoTranslate.X;
        _lastPhotoTranslateY = _photoTranslate.Y;
        _photoUserTransformDirty = userAdjusted;
        if (_rememberPhotoTransform && IsCrossPageDisplayActive())
        {
            _photoUnifiedTransformReady = true;
            SchedulePhotoUnifiedTransformSave();
            return;
        }
        if (_rememberPhotoTransform && _photoModeActive)
        {
            var key = GetCurrentPhotoTransformKey();
            if (!string.IsNullOrWhiteSpace(key))
            {
                _photoPageTransforms[key] = new PhotoTransformState(
                    _photoScale.ScaleX,
                    _photoScale.ScaleY,
                    _photoTranslate.X,
                    _photoTranslate.Y,
                    userAdjusted);
            }
        }
    }

    private void SchedulePhotoTransformSave(bool userAdjusted)
    {
        if (!_photoModeActive)
        {
            return;
        }
        _photoTransformSavePending = true;
        if (userAdjusted)
        {
            _photoTransformSaveUserAdjusted = true;
        }
        if (_photoTransformSaveTimer == null)
        {
            _photoTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PhotoTransformTimingDefaults.TransformSaveDebounceMs)
            };
            _photoTransformSaveTimer.Tick += OnPhotoTransformSaveTimerTick;
        }
        _photoTransformSaveTimer.Stop();
        _photoTransformSaveTimer.Start();
    }

    private void FlushPhotoTransformSave()
    {
        if (!_photoTransformSavePending)
        {
            return;
        }
        _photoTransformSaveTimer?.Stop();
        var adjusted = _photoTransformSaveUserAdjusted;
        _photoTransformSavePending = false;
        _photoTransformSaveUserAdjusted = false;
        SavePhotoTransformState(adjusted);
    }

    private void SchedulePhotoUnifiedTransformSave()
    {
        if (!IsCrossPageDisplayActive())
        {
            return;
        }
        _pendingUnifiedScaleX = _lastPhotoScaleX;
        _pendingUnifiedScaleY = _lastPhotoScaleY;
        _pendingUnifiedTranslateX = _lastPhotoTranslateX;
        _pendingUnifiedTranslateY = _lastPhotoTranslateY;
        if (_photoUnifiedTransformSaveTimer == null)
        {
            _photoUnifiedTransformSaveTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(PhotoTransformTimingDefaults.UnifiedTransformBroadcastDebounceMs)
            };
            _photoUnifiedTransformSaveTimer.Tick += OnPhotoUnifiedTransformSaveTimerTick;
        }
        _photoUnifiedTransformSaveTimer.Stop();
        _photoUnifiedTransformSaveTimer.Start();
    }

    private void OnPhotoTransformSaveTimerTick(object? sender, EventArgs e)
    {
        _photoTransformSaveTimer?.Stop();
        if (!_photoTransformSavePending)
        {
            _photoTransformSaveUserAdjusted = false;
            return;
        }
        var adjusted = _photoTransformSaveUserAdjusted;
        _photoTransformSavePending = false;
        _photoTransformSaveUserAdjusted = false;
        SavePhotoTransformState(adjusted);
    }

    private void OnPhotoUnifiedTransformSaveTimerTick(object? sender, EventArgs e)
    {
        _photoUnifiedTransformSaveTimer?.Stop();
        SafeActionExecutionExecutor.TryExecute(
            () => PhotoUnifiedTransformChanged?.Invoke(
                _pendingUnifiedScaleX,
                _pendingUnifiedScaleY,
                _pendingUnifiedTranslateX,
                _pendingUnifiedTranslateY),
            ex => Debug.WriteLine($"[PhotoUnifiedTransformChanged] callback failed: {ex.GetType().Name} - {ex.Message}"));
    }

}
