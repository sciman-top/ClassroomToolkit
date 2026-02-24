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
        var viewportHeight = ResolvePhotoViewportHeight();
        if (viewportHeight <= 1)
        {
            return false;
        }
        EnsurePhotoTransformsWritable();
        // Keep a small continuity overlap between editions for reading context.
        const double overlapRatio = 0.12;
        var step = Math.Max(24.0, viewportHeight * (1.0 - overlapRatio));
        var originalY = _photoTranslate.Y;
        _photoTranslate.Y -= direction * step;

        if (_crossPageDisplayEnabled)
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            SyncCurrentPageToViewportCenter();
            RequestCrossPageDisplayUpdate("step-viewport");
        }
        else
        {
            ClampSinglePageTranslateY(viewportHeight);
        }

        var moved = Math.Abs(_photoTranslate.Y - originalY) > 0.5;
        if (!moved)
        {
            return false;
        }
        SchedulePhotoTransformSave(userAdjusted: true);
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
        _photoWheelZoomBase = Math.Clamp(wheelBase, 1.0002, 1.0020);
        _photoGestureZoomSensitivity = Math.Clamp(gestureSensitivity, 0.5, 1.8);
    }

    private void ApplyPhotoScale(double scaleFactor, WpfPoint center)
    {
        EnsurePhotoTransformsWritable();
        double newScale = Math.Clamp(_photoScale.ScaleX * scaleFactor, 0.2, 4.0);
        if (Math.Abs(newScale - _photoScale.ScaleX) < 0.001)
        {
            return;
        }
        var before = ToPhotoSpace(center);
        _photoScale.ScaleX = newScale;
        _photoScale.ScaleY = newScale;
        _photoTranslate.X = center.X - before.X * newScale;
        _photoTranslate.Y = center.Y - before.Y * newScale;
        if (_crossPageDisplayEnabled)
        {
            ApplyCrossPageBoundaryLimits();
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate("apply-scale");
        }
    }

    private WpfPoint ToPhotoSpace(WpfPoint point)
    {
        if (!_photoModeActive)
        {
            return point;
        }
        var inverse = GetPhotoInverseMatrix();
        return inverse.Transform(point);
    }

    private Geometry? ToPhotoGeometry(Geometry geometry)
    {
        if (!_photoModeActive || geometry == null)
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
        if (!_photoModeActive || geometry == null)
        {
            return geometry;
        }
        var transform = GetPhotoMatrix();
        var clone = geometry.Clone();
        clone.Transform = new MatrixTransform(transform);
        if (clone.CanFreeze)
        {
            clone.Freeze();
        }
        return clone;
    }

    private Matrix GetPhotoMatrix()
    {
        var matrix = Matrix.Identity;
        matrix.Scale(_photoPageScale.ScaleX * _photoScale.ScaleX, _photoPageScale.ScaleY * _photoScale.ScaleY);
        matrix.Translate(_photoTranslate.X, _photoTranslate.Y);
        return matrix;
    }

    private Matrix GetPhotoInverseMatrix()
    {
        var scaleX = _photoPageScale.ScaleX * _photoScale.ScaleX;
        var scaleY = _photoPageScale.ScaleY * _photoScale.ScaleY;
        if (Math.Abs(scaleX) < 0.0001 || Math.Abs(scaleY) < 0.0001)
        {
            return Matrix.Identity;
        }
        var matrix = Matrix.Identity;
        matrix.Scale(1.0 / scaleX, 1.0 / scaleY);
        matrix.Translate(-_photoTranslate.X / scaleX, -_photoTranslate.Y / scaleY);
        return matrix;
    }

    private bool TryBeginPhotoPan(MouseButtonEventArgs e)
    {
        if (!_photoModeActive || _mode != PaintToolMode.Cursor || IsBoardActive())
        {
            return false;
        }
        BeginPhotoPan(e.GetPosition(OverlayRoot), captureStylus: false);
        e.Handled = true;
        return true;
    }

    private void BeginPhotoPan(WpfPoint position, bool captureStylus)
    {
        _photoPanning = true;
        _photoPanStart = position;
        _photoPanOriginX = _photoTranslate.X;
        _photoPanOriginY = _photoTranslate.Y;
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
        EnsurePhotoTransformsWritable();
        var delta = point - _photoPanStart;
        _photoTranslate.X = _photoPanOriginX + delta.X;
        _photoTranslate.Y = _photoPanOriginY + delta.Y;
        ApplyPhotoPanBounds(allowResistance: true);
        // Enable cross-page display when dragging vertically
        if (_crossPageDisplayEnabled)
        {
            if (Math.Abs(delta.Y) > 5)
            {
                _crossPageDragging = true;
            }
        }
        UpdateNeighborTransformsForPan();
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate("photo-pan");
        }
        SchedulePhotoTransformSave(userAdjusted: true);
    }

    private void EndPhotoPan()
    {
        if (!_photoPanning)
        {
            return;
        }
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
        if (_crossPageDragging && _crossPageDisplayEnabled)
        {
            _crossPageDragging = false;
            _crossPageTranslateClamped = false;
            FinalizeCurrentPageFromScroll();
        }
        LogPhotoInputTelemetry("pan-end", "commit");
        FlushPhotoTransformSave();
        RequestInkRedraw();
    }

    private void ApplyPhotoPanBounds(bool allowResistance)
    {
        if (!_photoModeActive || PhotoBackground.Source is not BitmapSource currentBitmap)
        {
            return;
        }

        if (_crossPageDisplayEnabled)
        {
            if (TryGetCrossPageBounds(
                    currentBitmap,
                    out var minX,
                    out var maxX,
                    out var minY,
                    out var maxY,
                    out _,
                    includeSlack: allowResistance))
            {
                var originalX = _photoTranslate.X;
                var originalY = _photoTranslate.Y;
                _photoTranslate.X = PhotoPanLimiter.ApplyAxis(_photoTranslate.X, minX, maxX, allowResistance);
                _photoTranslate.Y = PhotoPanLimiter.ApplyAxis(_photoTranslate.Y, minY, maxY, allowResistance);
                _crossPageTranslateClamped = Math.Abs(originalX - _photoTranslate.X) > 0.5
                    || Math.Abs(originalY - _photoTranslate.Y) > 0.5;
            }
            return;
        }

        if (TryGetSinglePagePanBounds(currentBitmap, out var singleMinX, out var singleMaxX, out var singleMinY, out var singleMaxY))
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
        out double maxY)
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

        if (pageWidth <= viewportWidth)
        {
            var centerX = (viewportWidth - pageWidth) * 0.5;
            minX = centerX;
            maxX = centerX;
        }
        else
        {
            minX = viewportWidth - pageWidth;
            maxX = 0;
        }

        if (pageHeight <= viewportHeight)
        {
            var centerY = (viewportHeight - pageHeight) * 0.5;
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
            _photoRestoreFullscreenPending = true;
            // Save current zoom/pan state before minimizing
            SavePhotoTransformState(true);
            return;
        }
        if (_photoRestoreFullscreenPending)
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
                if (!_crossPageDisplayEnabled && TryApplyStoredPhotoTransform(key))
                {
                }
                else
                {
                    EnsurePhotoTransformsWritable();
                    _photoScale.ScaleX = _lastPhotoScaleX;
                    _photoScale.ScaleY = _lastPhotoScaleY;
                    _photoTranslate.X = _lastPhotoTranslateX;
                    _photoTranslate.Y = _lastPhotoTranslateY;
                }
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
        if (_crossPageDisplayEnabled)
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
        if (_crossPageDisplayEnabled)
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
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _photoTransformSaveTimer.Tick += (_, _) =>
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
            };
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
        if (!_photoModeActive || !_crossPageDisplayEnabled)
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
                Interval = TimeSpan.FromMilliseconds(300)
            };
            _photoUnifiedTransformSaveTimer.Tick += (_, _) =>
            {
                _photoUnifiedTransformSaveTimer?.Stop();
                PhotoUnifiedTransformChanged?.Invoke(
                    _pendingUnifiedScaleX,
                    _pendingUnifiedScaleY,
                    _pendingUnifiedTranslateX,
                    _pendingUnifiedTranslateY);
            };
        }
        _photoUnifiedTransformSaveTimer.Stop();
        _photoUnifiedTransformSaveTimer.Start();
    }

    private double ResolvePhotoViewportWidth()
    {
        var viewportWidth = OverlayRoot.ActualWidth;
        if (viewportWidth <= 1)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
        }
        if (viewportWidth <= 1)
        {
            viewportWidth = ActualWidth;
        }
        if (viewportWidth <= 1)
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
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            return;
        }
        EnsurePhotoTransformsWritable();
        var imageWidth = GetBitmapDisplayWidthInDip(bitmap) * _photoPageScale.ScaleX;
        var imageHeight = GetBitmapDisplayHeightInDip(bitmap) * _photoPageScale.ScaleY;
        if (imageWidth <= 1 || imageHeight <= 1)
        {
            return;
        }
        var targetScale = Math.Clamp(viewportWidth / imageWidth, 0.2, 4.0);
        _photoScale.ScaleX = targetScale;
        _photoScale.ScaleY = targetScale;
        var scaledWidth = imageWidth * targetScale;
        var scaledHeight = imageHeight * targetScale;
        _photoTranslate.X = (viewportWidth - scaledWidth) * 0.5;
        _photoTranslate.Y = (viewportHeight - scaledHeight) * 0.5;
        if (_crossPageDisplayEnabled)
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            SyncCurrentPageToViewportCenter();
            RequestCrossPageDisplayUpdate("fit-width");
        }
        SchedulePhotoTransformSave(userAdjusted: true);
    }

    private double ResolvePhotoViewportHeight()
    {
        var viewportHeight = OverlayRoot.ActualHeight;
        if (viewportHeight <= 1)
        {
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }
        if (viewportHeight <= 1)
        {
            viewportHeight = ActualHeight;
        }
        if (viewportHeight <= 1)
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
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            viewportWidth = PhotoWindowFrame.ActualWidth;
            viewportHeight = PhotoWindowFrame.ActualHeight;
        }
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            var monitor = GetCurrentMonitorRectInDip(useWorkArea: false);
            viewportWidth = monitor.Width;
            viewportHeight = monitor.Height;
        }
        if (viewportWidth <= 1 || viewportHeight <= 1)
        {
            return;
        }
        double imageWidth;
        double imageHeight;
        if (_photoDocumentIsPdf)
        {
            var dpiX = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiX;
            var dpiY = dpiOverride.HasValue && dpiOverride.Value > 0 ? dpiOverride.Value : bitmap.DpiY;
            imageWidth = dpiX > 0 ? bitmap.PixelWidth * 96.0 / dpiX : bitmap.PixelWidth;
            imageHeight = dpiY > 0 ? bitmap.PixelHeight * 96.0 / dpiY : bitmap.PixelHeight;
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
        _photoTranslate.X = (viewportWidth - scaledWidth) / 2.0;
        _photoTranslate.Y = (viewportHeight - scaledHeight) / 2.0;
        SavePhotoTransformState(userAdjusted: false);
    }
}

