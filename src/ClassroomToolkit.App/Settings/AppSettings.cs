using System.Globalization;
using System.Windows.Media;
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
    public Color BrushColor { get; set; } = Colors.Red;
    public Color BoardColor { get; set; } = Colors.White;
    public PaintShapeType ShapeType { get; set; } = PaintShapeType.Line;

    public string BrushColorHex => ToHex(BrushColor);
    public string BoardColorHex => ToHex(BoardColor);

    public static Color ParseColor(string value, Color fallback)
    {
        try
        {
            var parsed = (Color)ColorConverter.ConvertFromString(value);
            return parsed;
        }
        catch
        {
            return fallback;
        }
    }

    private static string ToHex(Color color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }
}
