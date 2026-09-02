using System;
using System.Windows;
using System.Windows.Input;
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
        BeginPhotoPan(e.GetPosition(OverlayRoot), PhotoPanPointerKind.Mouse, captureStylus: false);
        e.Handled = true;
        return true;
    }

    private void BeginPhotoPan(WpfPoint position, PhotoPanPointerKind pointerKind, bool captureStylus)
    {
        StopPhotoWheelZoomAnimation(applyTarget: false, scheduleTransformSave: true);
        StopPhotoPanInertia(flushTransformSave: false, resetInkPanCompensation: false);
        _photoPanActivePointerKind = pointerKind;
        _photoPanning = true;
        _photoPanHadEffectiveMovement = false;
        _photoPanStart = position;
        _photoPanOriginX = _photoTranslate.X;
        _photoPanOriginY = _photoTranslate.Y;
        MarkPhotoInteractionForRenderQuality();
        ResetPhotoPanVelocitySamples(position);
        SyncPhotoInteractiveRefreshAnchor();
        LogPhotoInputTelemetry("pan-start", $"pointer={pointerKind}; stylus={captureStylus}");
        if (captureStylus)
        {
            Stylus.Capture(OverlayRoot);
        }
        else if (pointerKind != PhotoPanPointerKind.Touch)
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
            MarkPhotoInteractionForRenderQuality();
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
        if (_photoTouchPanDeviceId.HasValue)
        {
            OverlayRoot.ReleaseAllTouchCaptures();
            _photoTouchPanDeviceId = null;
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
        if (!inertiaStarted && (hadEffectiveMovement || hadCrossPageDragCommit))
        {
            MarkInkTransformVersionDirty();
            RequestInkRedraw();
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
