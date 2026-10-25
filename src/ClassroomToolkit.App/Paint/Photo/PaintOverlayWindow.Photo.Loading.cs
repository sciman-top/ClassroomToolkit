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
using ClassroomToolkit.App.Utilities;
using IoPath = System.IO.Path;
using MediaBrush = System.Windows.Media.Brush;
using MediaBrushes = System.Windows.Media.Brushes;
using WpfPoint = System.Windows.Point;
using WpfImage = System.Windows.Controls.Image;

namespace ClassroomToolkit.App.Paint;

public partial class PaintOverlayWindow
{
    private BitmapSource? TryLoadBitmapSource(
        string path,
        bool downsampleToMonitor = true,
        int targetDecodeWidth = 0)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }
        return PaintActionInvoker.TryInvoke<BitmapSource?>(() =>
        {
            using var stream = File.Open(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var effectiveDecodeWidth = 0;
            if (downsampleToMonitor)
            {
                effectiveDecodeWidth = targetDecodeWidth > 0
                    ? targetDecodeWidth
                    : ResolvePhotoDownsampleDecodeWidth();
            }
            var sourcePixelWidth = effectiveDecodeWidth > 0
                ? TryReadImagePixelWidth(stream)
                : 0;
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.StreamSource = stream;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

            // Limit decoding resolution to prevent OOM in cross-page mode.
            // Important: decode width must only downsample large sources, never upscale.
            if (effectiveDecodeWidth > 0)
            {
                if (sourcePixelWidth > effectiveDecodeWidth)
                {
                    bitmap.DecodePixelWidth = effectiveDecodeWidth;
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
        }, fallback: null);
    }

    private int ResolvePhotoDownsampleDecodeWidth()
    {
        // Use 1.5x of monitor width as a safe buffer for zooming.
        var monitorRect = GetCurrentMonitorRect();
        return monitorRect.Width > 0
            ? (int)Math.Round(monitorRect.Width * 1.5, MidpointRounding.AwayFromZero)
            : 0;
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

    private static int TryReadImagePixelWidth(Stream stream)
    {
        return PaintActionInvoker.TryInvoke(() =>
        {
            if (stream.CanSeek)
            {
                stream.Position = 0;
            }
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
            if (decoder.Frames.Count == 0)
            {
                return 0;
            }
            return decoder.Frames[0].PixelWidth;
        }, fallback: 0);
    }

    private bool TryBeginPhotoBackgroundOpenAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            PhotoBackground.Source = null;
            _photoBackgroundSourcePath = string.Empty;
            RefreshPhotoBackgroundVisibility();
            return false;
        }

        // 解码参数在 UI 线程预取（依赖窗口/显示器信息），整张照片的解码放到后台线程，
        // 避免大图在进入照片模式/换页时冻结 UI（与 PDF 路径的 StartPdfOpenAsync 同构）。
        var decodeDownsample = IsCrossPageDisplayActive();
        var decodeTargetWidth = decodeDownsample ? ResolvePhotoDownsampleDecodeWidth() : 0;
        var token = Interlocked.Increment(ref _photoLoadToken);
        var lifecycleToken = _overlayLifecycleCancellation.Token;

        _ = SafeTaskRunner.Run(
            "PaintOverlayWindow.PhotoBackgroundOpen",
            cancellationToken =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var bitmap = TryLoadBitmapSource(
                    imagePath,
                    downsampleToMonitor: decodeDownsample,
                    targetDecodeWidth: decodeTargetWidth);

                TryBeginInvoke(() =>
                {
                    if (token != _photoLoadToken || !_photoModeActive)
                    {
                        return;
                    }

                    if (bitmap == null)
                    {
                        var hadDisplayedPhoto = !string.IsNullOrEmpty(_photoBackgroundSourcePath);
                        PhotoBackground.Source = null;
                        _photoBackgroundSourcePath = string.Empty;
                        RefreshPhotoBackgroundVisibility();
                        if (!hadDisplayedPhoto)
                        {
                            // 首图即失败：没有可展示内容，退出照片模式回到画板。
                            ExitPhotoMode();
                        }
                        // 翻页中单图损坏/被占用：保留照片模式（屏上可能还有墨迹批注），
                        // 仅清空背景，教师可继续翻页或手动退出；与缩略图路径降级粒度一致。
                        return;
                    }

                    PhotoBackground.Source = bitmap;
                    _photoBackgroundSourcePath = imagePath;
                    RefreshPhotoBackgroundVisibility();
                    UpdateCurrentPageWidthNormalization(bitmap);
                    ApplyLoadedBitmapTransform(bitmap, useCrossPageUnifiedPath: IsCrossPageDisplayActive());
                    ApplyPendingPhotoCenter(bitmap, imagePath);
                }, DispatcherPriority.Render);
            },
            lifecycleToken,
            onError: ex => Debug.WriteLine(
                $"[PhotoOpen] async-open failed: {ex.GetType().Name} - {ex.Message}"));

        return true;
    }

    private void ApplyPendingPhotoCenter(BitmapSource bitmap, string sourcePath)
    {
        if (!_centerPhotoAtOriginalScaleWhenLoaded
            || !string.Equals(
                _centerPhotoAtOriginalScalePendingPath,
                sourcePath,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _centerPhotoAtOriginalScaleWhenLoaded = false;
        _centerPhotoAtOriginalScalePendingPath = string.Empty;
        CenterPhotoAtOriginalScale(bitmap);
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
