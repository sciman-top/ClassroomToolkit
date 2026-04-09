using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfPoint = System.Windows.Point;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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
}
