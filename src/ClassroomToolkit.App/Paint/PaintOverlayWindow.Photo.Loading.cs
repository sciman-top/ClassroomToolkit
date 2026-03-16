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
    private BitmapSource? TryLoadBitmapSource(string path, bool downsampleToMonitor = true)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            
            // Limit decoding resolution to prevent OOM in cross-page mode.
            // Important: decode width must only downsample large sources, never upscale.
            if (downsampleToMonitor)
            {
                // Use 1.5x of monitor width as a safe buffer for zooming.
                var monitorRect = GetCurrentMonitorRect();
                if (monitorRect.Width > 0)
                {
                    var targetDecodeWidth = (int)Math.Round(monitorRect.Width * 1.5, MidpointRounding.AwayFromZero);
                    var sourcePixelWidth = TryReadImagePixelWidth(path);
                    if (targetDecodeWidth > 0
                        && sourcePixelWidth > 0
                        && sourcePixelWidth > targetDecodeWidth)
                    {
                        bitmap.DecodePixelWidth = targetDecodeWidth;
                    }
                }
            }

            bitmap.EndInit();
            bitmap.Freeze();
            if (RequiresPixelFormatNormalization(bitmap.Format))
            {
                var converted = new FormatConvertedBitmap();
                converted.BeginInit();
                converted.Source = bitmap;
                converted.DestinationFormat = PixelFormats.Bgr32;
                converted.EndInit();
                converted.Freeze();
                return converted;
            }
            return bitmap;
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            return null;
        }
    }

    private static bool RequiresPixelFormatNormalization(PixelFormat format)
    {
        return format == PixelFormats.BlackWhite
               || format == PixelFormats.Indexed1
               || format == PixelFormats.Indexed2
               || format == PixelFormats.Indexed4
               || format == PixelFormats.Indexed8
               || format == PixelFormats.Gray2
               || format == PixelFormats.Gray4
               || format == PixelFormats.Gray8;
    }

    private static int TryReadImagePixelWidth(string path)
    {
        try
        {
            using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
            {
                return 0;
            }
            return decoder.Frames[0].PixelWidth;
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            return 0;
        }
    }

    private bool TrySetPhotoBackground(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            PhotoBackground.Source = null;
            RefreshPhotoBackgroundVisibility();
            return false;
        }
        try
        {
            var bitmap = TryLoadBitmapSource(imagePath, downsampleToMonitor: IsCrossPageDisplayActive());
            if (bitmap == null)
            {
                PhotoBackground.Source = null;
                RefreshPhotoBackgroundVisibility();
                return false;
            }
            PhotoBackground.Source = bitmap;
            RefreshPhotoBackgroundVisibility();
            UpdateCurrentPageWidthNormalization(bitmap);
            if (IsCrossPageDisplayActive())
            {
                ApplyLoadedBitmapTransform(bitmap, useCrossPageUnifiedPath: true);
                return true;
            }
            ApplyLoadedBitmapTransform(bitmap, useCrossPageUnifiedPath: false);
            return true;
        }
        catch (Exception caughtEx) when (ClassroomToolkit.App.AppGlobalExceptionHandlingPolicy.IsNonFatal(caughtEx))
        {
            PhotoBackground.Source = null;
            RefreshPhotoBackgroundVisibility();
            return false;
        }
    }

    private void ShowPhotoLoadingOverlay(string message)
    {
        _photoLoading = true;
        if (PhotoLoadingText != null)
        {
            PhotoLoadingText.Text = message;
        }
        if (PhotoLoadingOverlay != null)
        {
            PhotoLoadingOverlay.Visibility = Visibility.Visible;
        }
        UpdateOverlayHitTestVisibility();
    }

    private void HidePhotoLoadingOverlay()
    {
        _photoLoading = false;
        if (PhotoLoadingOverlay != null)
        {
            PhotoLoadingOverlay.Visibility = Visibility.Collapsed;
        }
        UpdateOverlayHitTestVisibility();
    }
}

