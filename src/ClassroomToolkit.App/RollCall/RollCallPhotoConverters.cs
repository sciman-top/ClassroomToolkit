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
/// 重复抽中同一学生免二次解码：小容量 LRU 以 (路径, 长度, 修改时间) 为键，
/// 课中替换照片靠元数据失效；仅 UI 线程访问（绑定转换器）。
/// </summary>
internal sealed class PhotoPathToImageSourceConverter : IValueConverter
{
    private const int PreviewDecodePixelWidth = 512;
    private const int CacheCapacity = 32;

    private readonly Dictionary<string, (long Length, long WriteTimeUtcTicks, BitmapSource Bitmap)> _decodeCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _decodeCacheOrder = new();

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

            var info = new FileInfo(path);
            var cacheKey = path;
            if (_decodeCache.TryGetValue(cacheKey, out var cached)
                && cached.Length == info.Length
                && cached.WriteTimeUtcTicks == info.LastWriteTimeUtc.Ticks)
            {
                TouchCacheOrder(cacheKey);
                return cached.Bitmap;
            }

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.DecodePixelWidth = PreviewDecodePixelWidth;
            bitmap.EndInit();
            bitmap.Freeze();

            StoreInCache(cacheKey, (info.Length, info.LastWriteTimeUtc.Ticks, bitmap));
            return bitmap;
        }
        catch (Exception ex) when (ClassroomToolkit.App.AppGlobalExceptionHandling.IsNonFatal(ex))
        {
            System.Diagnostics.Debug.WriteLine($"[RollCallWindow] Photo preview decode failed: {path}. Error: {ex.Message}");
            return null;
        }
    }

    private void TouchCacheOrder(string cacheKey)
    {
        var node = _decodeCacheOrder.Find(cacheKey);
        if (node != null)
        {
            _decodeCacheOrder.Remove(node);
            _decodeCacheOrder.AddFirst(node);
        }
    }

    private void StoreInCache(string cacheKey, (long Length, long WriteTimeUtcTicks, BitmapSource Bitmap) entry)
    {
        while (_decodeCache.Count >= CacheCapacity)
        {
            var oldest = _decodeCacheOrder.Last;
            if (oldest == null)
            {
                _decodeCache.Clear();
                break;
            }

            _decodeCache.Remove(oldest.Value);
            _decodeCacheOrder.RemoveLast();
        }

        _decodeCache[cacheKey] = entry;
        _decodeCacheOrder.AddFirst(cacheKey);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException("PhotoPathToImageSourceConverter is one-way.");
    }
}
