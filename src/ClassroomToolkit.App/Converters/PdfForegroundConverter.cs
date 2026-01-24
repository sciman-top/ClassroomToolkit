using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Media = System.Windows.Media;

namespace ClassroomToolkit.App.Converters;

public sealed class PdfForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isPdf && isPdf ? new Media.SolidColorBrush(Media.Color.FromRgb(255, 145, 0)) : DependencyProperty.UnsetValue;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
