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

    private bool TryBeginPhotoPan(MouseButtonEventArgs e)
    {
        var shouldPanPhoto = StylusCursorPolicy.ShouldPanPhoto(
            _photoModeActive,
            IsBoardActive(),
            _mode,
            IsInkOperationActive());
        if (!PhotoPanBeginGuardPolicy.ShouldBegin(shouldPanPhoto, _photoPanning))
        {
            return false;
        }
        BeginPhotoPan(e.GetPosition(OverlayRoot), captureStylus: false);
        e.Handled = true;
        return true;
    }

    private void BeginPhotoPan(WpfPoint position, bool captureStylus)
    {
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        _photoPanning = true;
        _photoPanHadEffectiveMovement = false;
        _photoPanStart = position;
        _photoPanOriginX = _photoTranslate.X;
        _photoPanOriginY = _photoTranslate.Y;
        ResetPhotoPanVelocitySamples(position);
        SyncPhotoInteractiveRefreshAnchor();
        LogPhotoInputTelemetry("pan-start", $"stylus={captureStylus}");
        if (captureStylus)
        {
            Stylus.Capture(OverlayRoot);
        }
        else
        {
            OverlayRoot.CaptureMouse();
        }
    }

    private void UpdatePhotoPan(WpfPoint point)
    {
        if (!_photoPanning)
        {
            return;
        }

        UpdatePhotoPanVelocitySamples(point);
        EnsurePhotoTransformsWritable();
        var delta = point - _photoPanStart;
        _photoTranslate.X = _photoPanOriginX + delta.X;
        _photoTranslate.Y = _photoPanOriginY + delta.Y;
        ApplyPhotoPanBounds(allowResistance: true);
        var movedSincePanStart =
            Math.Abs(_photoTranslate.X - _photoPanOriginX) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip
            || Math.Abs(_photoTranslate.Y - _photoPanOriginY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        if (movedSincePanStart)
        {
            _photoPanHadEffectiveMovement = true;
        }
        UpdatePhotoInkPanCompensation();
        var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
            _lastPhotoInteractiveRefreshTranslateX,
            _lastPhotoInteractiveRefreshTranslateY,
            _photoTranslate.X,
            _photoTranslate.Y);
        // Enable cross-page drag mode only when vertical drag exceeds threshold.
        if (shouldRefresh && PhotoPanDragActivationPolicy.ShouldActivateCrossPageDrag(
                IsCrossPageDisplayActive(),
                delta.Y))
        {
            _crossPageDragging = true;
        }
        if (!shouldRefresh)
        {
            return;
        }
        SyncPhotoInteractiveRefreshAnchor();

        UpdateNeighborTransformsForPan();
        if (IsCrossPageDisplayActive())
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.PhotoPan);
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        if (PhotoInkPanRedrawPolicy.ShouldRequest(
                IsPhotoInkModeActive(),
                _photoTranslate.X,
                _photoTranslate.Y,
                _lastInkRedrawPhotoTranslateX,
                _lastInkRedrawPhotoTranslateY))
        {
            RequestPhotoTransformInkRedraw();
        }
    }

    private void EndPhotoPan(bool allowInertia = true)
    {
        if (!_photoPanning)
        {
            return;
        }
        var hadEffectiveMovement = _photoPanHadEffectiveMovement;
        var hadCrossPageDragCommit = _crossPageDragging && IsCrossPageDisplayActive();
        _photoPanning = false;
        if (OverlayRoot.IsMouseCaptured)
        {
            OverlayRoot.ReleaseMouseCapture();
        }
        if (OverlayRoot.IsStylusCaptured)
        {
            Stylus.Capture(null);
        }
        ApplyPhotoPanBounds(allowResistance: false);
        if (_crossPageDragging && IsCrossPageDisplayActive())
        {
            _crossPageDragging = false;
            _crossPageTranslateClamped = false;
            FinalizeCurrentPageFromScroll();
        }
        var inertiaStarted = allowInertia
            && hadEffectiveMovement
            && TryStartPhotoPanInertiaFromRelease();
        _photoPanHadEffectiveMovement = false;
        LogPhotoInputTelemetry("pan-end", "commit");
        if (!inertiaStarted)
        {
            FlushPhotoTransformSave();
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        }
        if (PhotoPanEndRedrawPolicy.ShouldRequestInkRedraw(
                hadEffectiveMovement && !inertiaStarted,
                hadCrossPageDragCommit && !inertiaStarted))
        {
            MarkInkTransformVersionDirty();
            RequestInkRedraw();
        }
    }

    private void ResetPhotoPanVelocitySamples(WpfPoint position)
    {
        var nowTicks = Stopwatch.GetTimestamp();
        _photoPanVelocitySamples.Clear();
        _photoPanVelocitySamples.Add(new PhotoPanVelocitySample(position, nowTicks));
    }

    private void UpdatePhotoPanVelocitySamples(WpfPoint position)
    {
        var nowTicks = Stopwatch.GetTimestamp();
        if (_photoPanVelocitySamples.Count <= 0)
        {
            ResetPhotoPanVelocitySamples(position);
            return;
        }

        var lastTimestampTicks = _photoPanVelocitySamples[^1].TimestampTicks;
        if (nowTicks <= lastTimestampTicks)
        {
            nowTicks = lastTimestampTicks + 1;
        }

        _photoPanVelocitySamples.Add(new PhotoPanVelocitySample(position, nowTicks));
        TrimPhotoPanVelocitySamples(nowTicks);
    }

    private void TrimPhotoPanVelocitySamples(long nowTicks)
    {
        var maxAgeTicks = (long)Math.Ceiling(
            PhotoPanInertiaDefaults.MouseVelocitySampleHistoryMaxAgeMs * Stopwatch.Frequency / 1000.0);
        while (_photoPanVelocitySamples.Count > 1
               && nowTicks - _photoPanVelocitySamples[0].TimestampTicks > maxAgeTicks)
        {
            _photoPanVelocitySamples.RemoveAt(0);
        }

        while (_photoPanVelocitySamples.Count > PhotoPanInertiaDefaults.MouseVelocitySampleCapacity)
        {
            _photoPanVelocitySamples.RemoveAt(0);
        }
    }

    private bool TryStartPhotoPanInertiaFromRelease()
    {
        var nowTicks = Stopwatch.GetTimestamp();
        if (!PhotoPanInertiaMotionPolicy.TryResolveReleaseVelocity(
                _photoPanVelocitySamples,
                nowTicks,
                Stopwatch.Frequency,
                _photoPanInertiaTuning,
                out var velocityDipPerMs))
        {
            return false;
        }

        _photoPanInertiaVelocityDipPerMs = velocityDipPerMs;
        var nowUtc = GetCurrentUtcTimestamp();
        _photoPanInertiaLastTickUtc = nowUtc;
        _photoPanInertiaStartUtc = nowUtc;
        _photoPanInertiaLastRenderingTime = TimeSpan.MinValue;
        if (!_photoPanInertiaRenderingAttached)
        {
            CompositionTarget.Rendering += OnPhotoPanInertiaRendering;
            _photoPanInertiaRenderingAttached = true;
        }
        LogPhotoInputTelemetry(
            "pan-inertia-start",
            $"vx={_photoPanInertiaVelocityDipPerMs.X:0.###},vy={_photoPanInertiaVelocityDipPerMs.Y:0.###}");
        return true;
    }

    private void OnPhotoPanInertiaRendering(object? sender, EventArgs e)
    {
        if (!_photoModeActive || _photoPanning || _photoPanInertiaLastTickUtc == PhotoInputConflictDefaults.UnsetTimestampUtc)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
            return;
        }

        var nowUtc = GetCurrentUtcTimestamp();
        if (_photoPanInertiaStartUtc != PhotoInputConflictDefaults.UnsetTimestampUtc)
        {
            var durationMs = (nowUtc - _photoPanInertiaStartUtc).TotalMilliseconds;
            if (PhotoPanInertiaMotionPolicy.ShouldStopByDuration(durationMs, _photoPanInertiaTuning))
            {
                StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
                return;
            }
        }

        var fallbackElapsedMs = (nowUtc - _photoPanInertiaLastTickUtc).TotalMilliseconds;
        double elapsedMs = fallbackElapsedMs;
        if (e is RenderingEventArgs renderingArgs)
        {
            if (_photoPanInertiaLastRenderingTime != TimeSpan.MinValue
                && renderingArgs.RenderingTime > _photoPanInertiaLastRenderingTime)
            {
                elapsedMs = (renderingArgs.RenderingTime - _photoPanInertiaLastRenderingTime).TotalMilliseconds;
            }
            _photoPanInertiaLastRenderingTime = renderingArgs.RenderingTime;
        }

        elapsedMs = PhotoPanInertiaMotionPolicy.ResolveFrameElapsedMilliseconds(elapsedMs);
        if (elapsedMs <= 0)
        {
            return;
        }
        _photoPanInertiaLastTickUtc = nowUtc;

        var translation = PhotoPanInertiaMotionPolicy.ResolveTranslation(
            _photoPanInertiaVelocityDipPerMs,
            elapsedMs,
            _photoPanInertiaTuning);
        if (translation.LengthSquared <= 0)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
            return;
        }

        EnsurePhotoTransformsWritable();
        var beforeX = _photoTranslate.X;
        var beforeY = _photoTranslate.Y;
        _photoTranslate.X += translation.X;
        _photoTranslate.Y += translation.Y;
        ApplyPhotoPanBounds(allowResistance: false);
        var moved = Math.Abs(_photoTranslate.X - beforeX) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip
            || Math.Abs(_photoTranslate.Y - beforeY) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
        if (!moved)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
            return;
        }

        UpdatePhotoInkPanCompensation();
        var shouldRefresh = PhotoPanInteractiveRefreshPolicy.ShouldRefresh(
            _lastPhotoInteractiveRefreshTranslateX,
            _lastPhotoInteractiveRefreshTranslateY,
            _photoTranslate.X,
            _photoTranslate.Y);
        if (shouldRefresh)
        {
            SyncPhotoInteractiveRefreshAnchor();
            UpdateNeighborTransformsForPan();
            if (PhotoInkPanRedrawPolicy.ShouldRequest(
                    IsPhotoInkModeActive(),
                    _photoTranslate.X,
                    _photoTranslate.Y,
                    _lastInkRedrawPhotoTranslateX,
                    _lastInkRedrawPhotoTranslateY))
            {
                RequestPhotoTransformInkRedraw();
            }
        }
        if (IsCrossPageDisplayActive())
        {
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.PhotoPan);
        }
        SchedulePhotoTransformSave(userAdjusted: true);

        _photoPanInertiaVelocityDipPerMs = PhotoPanInertiaMotionPolicy.ResolveVelocityAfterDeceleration(
            _photoPanInertiaVelocityDipPerMs,
            elapsedMs,
            _photoPanInertiaTuning);
        if (_photoPanInertiaVelocityDipPerMs.LengthSquared <= 0)
        {
            StopPhotoPanInertia(flushTransformSave: true, resetInkPanCompensation: true);
        }
    }

    private void StopPhotoPanInertia(bool flushTransformSave, bool resetInkPanCompensation)
    {
        if (_photoPanInertiaRenderingAttached)
        {
            CompositionTarget.Rendering -= OnPhotoPanInertiaRendering;
            _photoPanInertiaRenderingAttached = false;
        }
        _photoPanInertiaVelocityDipPerMs = default;
        _photoPanInertiaLastTickUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
        _photoPanInertiaStartUtc = PhotoInputConflictDefaults.UnsetTimestampUtc;
        _photoPanInertiaLastRenderingTime = TimeSpan.MinValue;
        if (flushTransformSave)
        {
            FlushPhotoTransformSave();
        }
        if (resetInkPanCompensation)
        {
            ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        }
    }

    private void ApplyPhotoPanBounds(bool allowResistance)
    {
        if (!_photoModeActive || PhotoBackground.Source is not BitmapSource currentBitmap)
        {
            return;
        }

        if (IsCrossPageDisplayActive())
        {
            if (TryGetCrossPageBounds(
                    currentBitmap,
                    out var minX,
                    out var maxX,
                    out var minY,
                    out var maxY,
                    out _,
                    includeSlack: allowResistance,
                    preferCachedDuringInteraction: allowResistance))
            {
                var originalX = _photoTranslate.X;
                var originalY = _photoTranslate.Y;
                _photoTranslate.X = PhotoPanLimiter.ApplyAxis(_photoTranslate.X, minX, maxX, allowResistance);
                _photoTranslate.Y = PhotoPanLimiter.ApplyAxis(_photoTranslate.Y, minY, maxY, allowResistance);
                _crossPageTranslateClamped =
                    Math.Abs(originalX - _photoTranslate.X) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip
                    || Math.Abs(originalY - _photoTranslate.Y) > CrossPageViewportBoundsDefaults.TranslateClampEpsilonDip;
            }
            return;
        }

        if (TryGetSinglePagePanBounds(
                currentBitmap,
                out var singleMinX,
                out var singleMaxX,
                out var singleMinY,
                out var singleMaxY,
                includeSlack: allowResistance))
        {
            _photoTranslate.X = PhotoPanLimiter.ApplyAxis(_photoTranslate.X, singleMinX, singleMaxX, allowResistance);
            _photoTranslate.Y = PhotoPanLimiter.ApplyAxis(_photoTranslate.Y, singleMinY, singleMaxY, allowResistance);
        }
    }

    private bool TryGetSinglePagePanBounds(
        BitmapSource bitmap,
        out double minX,
        out double maxX,
        out double minY,
        out double maxY,
        bool includeSlack)
    {
        minX = maxX = minY = maxY = 0;
        var viewportWidth = OverlayRoot.ActualWidth;
        if (viewportWidth <= 0)
        {
            viewportWidth = ActualWidth;
        }
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 0)
        {
            viewportHeight = ActualHeight;
        }
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            return false;
        }

        var pageWidth = GetBitmapDisplayWidthInDip(bitmap) * _photoPageScale.ScaleX * _photoScale.ScaleX;
        var pageHeight = GetBitmapDisplayHeightInDip(bitmap) * _photoPageScale.ScaleY * _photoScale.ScaleY;
        if (pageWidth <= 0 || pageHeight <= 0)
        {
            return false;
        }

        var xRange = PhotoHorizontalPanRangePolicy.Resolve(
            viewportWidth,
            pageWidth,
            includeSlack);
        minX = xRange.MinX;
        maxX = xRange.MaxX;

        if (pageHeight <= viewportHeight)
        {
            var centerY = (viewportHeight - pageHeight) * CrossPageViewportBoundsDefaults.CenterRatio;
            minY = centerY;
            maxY = centerY;
        }
        else
        {
            minY = viewportHeight - pageHeight;
            maxY = 0;
        }

        return true;
    }

    private void EnsurePhotoTransformsWritable()
    {
        if (_photoPageScale.IsFrozen)
        {
            _photoPageScale = _photoPageScale.Clone();
        }
        if (_photoScale.IsFrozen)
        {
            _photoScale = _photoScale.Clone();
        }
        if (_photoTranslate.IsFrozen)
        {
            _photoTranslate = _photoTranslate.Clone();
        }
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

    private double ResolvePhotoViewportWidth()
    {
        var viewportWidth = OverlayRoot.ActualWidth;
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
        }
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportWidth = ActualWidth;
        }
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportWidth = monitor.Width;
        }
        return viewportWidth;
    }

    private void OnPhotoFitWidthClick(object sender, RoutedEventArgs e)
    {
        if (!_photoModeActive || PhotoBackground.Source is not BitmapSource bitmap)
        {
            return;
        }
        FitPhotoWidthAndCenter(bitmap);
        if (e.RoutedEvent != null)
        {
            e.Handled = true;
        }
    }

    private void FitPhotoWidthAndCenter(BitmapSource bitmap)
    {
        var viewportWidth = ResolvePhotoViewportWidth();
        var viewportHeight = ResolvePhotoViewportHeight();
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var imageWidth = GetBitmapDisplayWidthInDip(bitmap) * _photoPageScale.ScaleX;
        var imageHeight = GetBitmapDisplayHeightInDip(bitmap) * _photoPageScale.ScaleY;
        if (imageWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || imageHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return;
        }
        var targetScale = Math.Clamp(
            viewportWidth / imageWidth,
            PhotoTransformViewportDefaults.MinScale,
            PhotoTransformViewportDefaults.MaxScale);
        _photoScale.ScaleX = targetScale;
        _photoScale.ScaleY = targetScale;
        var scaledWidth = imageWidth * targetScale;
        var scaledHeight = imageHeight * targetScale;
        _photoTranslate.X = (viewportWidth - scaledWidth) * CrossPageViewportBoundsDefaults.CenterRatio;
        _photoTranslate.Y = (viewportHeight - scaledHeight) * CrossPageViewportBoundsDefaults.CenterRatio;
        if (IsCrossPageDisplayActive())
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            SyncCurrentPageToViewportCenter();
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.FitWidth);
        }
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        SchedulePhotoTransformSave(userAdjusted: true);
        RequestPhotoTransformInkRedraw();
    }

    private double ResolvePhotoViewportHeight()
    {
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }
        if (viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportHeight = ActualHeight;
        }
        if (viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportHeight = monitor.Height;
        }
        return viewportHeight;
    }

    private void ApplyPhotoFitToViewport(BitmapSource bitmap, double? dpiOverride = null)
    {
        if (bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var viewportWidth = OverlayRoot.ActualWidth;
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportWidth = monitor.Width;
            viewportHeight = monitor.Height;
        }
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return;
        }
        double imageWidth;
        double imageHeight;
        if (_photoDocumentIsPdf)
        {
            var dpiX = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiX;
            var dpiY = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiY;
            imageWidth = dpiX > 0 ? bitmap.PixelWidth * PhotoDocumentRuntimeDefaults.PdfDefaultDpi / dpiX : bitmap.PixelWidth;
            imageHeight = dpiY > 0 ? bitmap.PixelHeight * PhotoDocumentRuntimeDefaults.PdfDefaultDpi / dpiY : bitmap.PixelHeight;
        }
        else
        {
            imageWidth = GetBitmapDisplayWidthInDip(bitmap);
            imageHeight = GetBitmapDisplayHeightInDip(bitmap);
        }

        if (bitmap is BitmapImage bi && (bi.Rotation == Rotation.Rotate90 || bi.Rotation == Rotation.Rotate270))
        {
            (imageWidth, imageHeight) = (imageHeight, imageWidth);
        }

        var scaleX = viewportWidth / imageWidth;
        var scaleY = viewportHeight / imageHeight;
        var scale = Math.Min(scaleX, scaleY);
        _photoScale.ScaleX = scale;
        _photoScale.ScaleY = scale;
        var scaledWidth = imageWidth * scale;
        var scaledHeight = imageHeight * scale;
        _photoTranslate.X = (viewportWidth - scaledWidth) * CrossPageViewportBoundsDefaults.CenterRatio;
        _photoTranslate.Y = (viewportHeight - scaledHeight) * CrossPageViewportBoundsDefaults.CenterRatio;
        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        SavePhotoTransformState(userAdjusted: false);
        RequestPhotoTransformInkRedraw();
    }
}
