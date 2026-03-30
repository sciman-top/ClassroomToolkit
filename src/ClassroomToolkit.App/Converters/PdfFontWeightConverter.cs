using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClassroomToolkit.App.Converters;

public sealed class PdfFontWeightConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is bool isPdf && isPdf ? FontWeights.Bold : FontWeights.Normal;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
