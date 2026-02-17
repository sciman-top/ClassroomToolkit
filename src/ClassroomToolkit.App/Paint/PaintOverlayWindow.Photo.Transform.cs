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
            RequestCrossPageDisplayUpdate();
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
        RequestInkRedraw();
        return true;
    }

    private void ZoomPhoto(int delta, WpfPoint center)
    {
        double scaleFactor = Math.Pow(PhotoWheelZoomBase, delta);
        ApplyPhotoScale(scaleFactor, center);
        RequestInkRedraw();
    }

    private void ZoomPhotoByFactor(double scaleFactor)
    {
        var center = new WpfPoint(OverlayRoot.ActualWidth / 2.0, OverlayRoot.ActualHeight / 2.0);
        ApplyPhotoScale(scaleFactor, center);
        RequestInkRedraw();
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
            RequestCrossPageDisplayUpdate();
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
        _photoPanning = true;
        _photoPanStart = e.GetPosition(OverlayRoot);
        _photoPanOriginX = _photoTranslate.X;
        _photoPanOriginY = _photoTranslate.Y;
        OverlayRoot.CaptureMouse();
        e.Handled = true;
        return true;
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
        // Enable cross-page display when dragging vertically
        if (_crossPageDisplayEnabled)
        {
            if (Math.Abs(delta.Y) > 5)
            {
                _crossPageDragging = true;
            }
            ApplyCrossPageBoundaryLimits();
        }
        UpdateNeighborTransformsForPan();
        if (_crossPageDisplayEnabled)
        {
            RequestCrossPageDisplayUpdate();
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        RequestInkRedraw();
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
        if (_crossPageDragging && _crossPageDisplayEnabled)
        {
            _crossPageDragging = false;
            _crossPageTranslateClamped = false;
            FinalizeCurrentPageFromScroll();
        }
        FlushPhotoTransformSave();
        RequestInkRedraw();
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
            RequestCrossPageDisplayUpdate();
        }
        SchedulePhotoTransformSave(userAdjusted: true);
        RequestInkRedraw();
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
        RequestInkRedraw();
    }
}
