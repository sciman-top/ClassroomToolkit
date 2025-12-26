using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClassroomToolkit.App.Converters;

public sealed class BoolToGridLengthConverter : IValueConverter
{
    public GridLength TrueLength { get; set; } = new(1, GridUnitType.Star);
    public GridLength FalseLength { get; set; } = new(0);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var flag = value is bool b && b;
        return flag ? TrueLength : FalseLength;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is GridLength length)
        {
            return length.Value > 0;
        }
        return false;
    }
}
