using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace ClassroomToolkit.App.RollCall;

/// <summary>
/// 点名窗照片预览的绑定转换器：<see cref="CurrentStudentPhotoPath"/> 是路径字符串，
/// 不能走 BooleanToVisibilityConverter（转换失败会让照片区永久 Collapsed）。
/// </summary>
internal sealed class PhotoPathToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var hasPath = value is string path && !string.IsNullOrWhiteSpace(path);
        return hasPath ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("PhotoPathToVisibilityConverter is one-way.");
    }
}

/// <summary>
/// 照片路径 → 降采样 ImageSource。默认字符串绑定会全尺寸解码原图，
/// 预览可见后点名切人时在 UI 线程产生明显卡顿，这里按预览尺寸解码并冻结。
/// </summary>
internal sealed class PhotoPathToImageSourceConverter : IValueConverter
{
    private const int PreviewDecodePixelWidth = 512;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = PreviewDecodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"[RollCallWindow] Photo preview decode failed: {path}. Error: {ex.Message}");
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("PhotoPathToImageSourceConverter is one-way.");
    }
}
