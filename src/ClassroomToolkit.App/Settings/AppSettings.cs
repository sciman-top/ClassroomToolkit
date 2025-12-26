using System.Globalization;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaColors = System.Windows.Media.Colors;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Settings;

public sealed class AppSettings
{
    public string RemotePresenterKey { get; set; } = "tab";
    public string WpsInputMode { get; set; } = "auto";
    public bool WpsWheelForward { get; set; } = true;

    public double BrushSize { get; set; } = 12;
    public double EraserSize { get; set; } = 24;
    public byte BrushOpacity { get; set; } = 255;
    public byte BoardOpacity { get; set; } = 0;
    public MediaColor BrushColor { get; set; } = MediaColors.Red;
    public MediaColor BoardColor { get; set; } = MediaColors.White;
    public PaintShapeType ShapeType { get; set; } = PaintShapeType.Line;

    public string BrushColorHex => ToHex(BrushColor);
    public string BoardColorHex => ToHex(BoardColor);

    public static MediaColor ParseColor(string value, MediaColor fallback)
    {
        try
        {
            var parsed = (MediaColor)MediaColorConverter.ConvertFromString(value);
            return parsed;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToHex(MediaColor color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }
}
