using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Media = System.Windows.Media;

namespace ClassroomToolkit.App.Converters;

public sealed class PdfForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // PDF: Amber/Warning color (#F59E0B) - 醒目的琥珀色
        // 其他: Slate 100 (#F1F5F9) - 浅色文字
        return value is bool isPdf && isPdf 
            ? new Media.SolidColorBrush(Media.Color.FromRgb(245, 158, 11))  // #F59E0B
            : new Media.SolidColorBrush(Media.Color.FromRgb(241, 245, 249)); // #F1F5F9
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
