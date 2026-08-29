using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Media = System.Windows.Media;

namespace ClassroomToolkit.App.Converters;

public sealed class PdfForegroundConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value is bool isPdf && isPdf
            ? "CTK.Brush.Warning"
            : "CTK.Brush.Text.Primary";

        // Return the shared semantic brush so an in-place theme switch updates
        // existing PDF/file labels without rebuilding the image manager view.
        return System.Windows.Application.Current?.TryFindResource(resourceKey) as Media.Brush
            ?? (value is bool isPdfFallback && isPdfFallback
                ? System.Windows.SystemColors.HighlightBrush
                : System.Windows.SystemColors.ControlTextBrush);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return System.Windows.Data.Binding.DoNothing;
    }
}
