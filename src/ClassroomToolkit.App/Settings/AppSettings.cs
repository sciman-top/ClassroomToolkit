using System.Globalization;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaColors = System.Windows.Media.Colors;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Settings;

public sealed class AppSettings
{
    public bool RollCallShowId { get; set; } = true;
    public bool RollCallShowName { get; set; } = true;
    public bool RollCallRemoteEnabled { get; set; } = false;
    public bool RollCallShowPhoto { get; set; } = false;
    public int RollCallPhotoDurationSeconds { get; set; } = 0;
    public string RollCallPhotoSharedClass { get; set; } = string.Empty;
    public bool RollCallTimerSoundEnabled { get; set; } = true;
    public string RollCallTimerSoundVariant { get; set; } = "gentle";
    public bool RollCallTimerReminderEnabled { get; set; } = false;
    public int RollCallTimerReminderIntervalMinutes { get; set; } = 0;
    public string RollCallTimerReminderSoundVariant { get; set; } = "soft_beep";
    public string RollCallMode { get; set; } = "roll_call";
    public string RollCallTimerMode { get; set; } = "countdown";
    public int RollCallTimerMinutes { get; set; } = 5;
    public int RollCallTimerSeconds { get; set; } = 0;
    public int RollCallTimerSecondsLeft { get; set; } = 300;
    public int RollCallStopwatchSeconds { get; set; } = 0;
    public bool RollCallTimerRunning { get; set; } = false;
    public bool RollCallSpeechEnabled { get; set; } = false;
    public string RollCallSpeechEngine { get; set; } = "pyttsx3";
    public string RollCallSpeechVoiceId { get; set; } = string.Empty;
    public string RollCallSpeechOutputId { get; set; } = string.Empty;
    public string RemotePresenterKey { get; set; } = "tab";
    public string RollCallCurrentClass { get; set; } = string.Empty;
    public bool ControlMsPpt { get; set; } = true;
    public bool ControlWpsPpt { get; set; } = true;
    public string WpsInputMode { get; set; } = "auto";
    public bool WpsWheelForward { get; set; } = true;
    public int LauncherX { get; set; } = 120;
    public int LauncherY { get; set; } = 120;
    public int LauncherBubbleX { get; set; } = 120;
    public int LauncherBubbleY { get; set; } = 120;
    public bool LauncherMinimized { get; set; } = false;
    public int LauncherAutoExitSeconds { get; set; } = 0;

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
