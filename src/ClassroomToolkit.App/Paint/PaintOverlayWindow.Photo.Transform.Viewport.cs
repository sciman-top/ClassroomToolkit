using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ClassroomToolkit.App.Photos;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
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

    public void CenterPhotoAtOriginalScale()
    {
        if (!_photoModeActive || PhotoBackground.Source is not BitmapSource bitmap)
        {
            return;
        }

        var viewportWidth = ResolvePhotoViewportWidth();
        var viewportHeight = ResolvePhotoViewportHeight();
        if (viewportWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || viewportHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return;
        }

        var imageWidth = GetBitmapDisplayWidthInDip(bitmap) * _photoPageScale.ScaleX;
        var imageHeight = GetBitmapDisplayHeightInDip(bitmap) * _photoPageScale.ScaleY;
        if (imageWidth <= PhotoTransformViewportDefaults.MinUsableViewportDip
            || imageHeight <= PhotoTransformViewportDefaults.MinUsableViewportDip)
        {
            return;
        }

        EnsurePhotoTransformsWritable();
        _photoScale.ScaleX = PhotoTransformViewportDefaults.DefaultScale;
        _photoScale.ScaleY = PhotoTransformViewportDefaults.DefaultScale;
        _photoTranslate.X = (viewportWidth - imageWidth) * CrossPageViewportBoundsDefaults.CenterRatio;
        _photoTranslate.Y = (viewportHeight - imageHeight) * CrossPageViewportBoundsDefaults.CenterRatio;
        if (IsCrossPageDisplayActive())
        {
            ApplyCrossPageBoundaryLimits(includeSlack: false);
            SyncCurrentPageToViewportCenter();
            RequestCrossPageDisplayUpdate(CrossPageUpdateSources.FitWidth);
        }

        ResetPhotoInkPanCompensation(syncToCurrentPhotoTranslate: false);
        SyncPhotoInteractiveRefreshAnchor();
        SavePhotoTransformState(userAdjusted: true);
        RequestPhotoTransformInkRedraw();
    }
}
