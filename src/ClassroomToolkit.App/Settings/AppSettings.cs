using System.Collections.Generic;
using System.Globalization;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using MediaColors = System.Windows.Media.Colors;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Ink;

namespace ClassroomToolkit.App.Settings;

public sealed class AppSettings
{
    public const int UnsetPosition = int.MinValue;

    public bool RollCallShowId { get; set; } = true;
    public bool RollCallShowName { get; set; } = true;
    public bool RollCallRemoteEnabled { get; set; } = false;
    public bool RollCallRemoteGroupSwitchEnabled { get; set; } = false;
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
    public int RollCallWindowX { get; set; } = UnsetPosition;
    public int RollCallWindowY { get; set; } = UnsetPosition;
    public int RollCallWindowWidth { get; set; }
    public int RollCallWindowHeight { get; set; }
    public int RollCallIdFontSize { get; set; } = 48;
    public int RollCallNameFontSize { get; set; } = 60;
    public int RollCallTimerFontSize { get; set; } = 56;
    public bool RollCallSpeechEnabled { get; set; } = false;
    public string RollCallSpeechEngine { get; set; } = "pyttsx3";
    public string RollCallSpeechVoiceId { get; set; } = string.Empty;
    public string RollCallSpeechOutputId { get; set; } = string.Empty;
    public string RemotePresenterKey { get; set; } = "tab";
    public string RemoteGroupSwitchKey { get; set; } = "b";
    public string RollCallCurrentClass { get; set; } = string.Empty;
    public string RollCallCurrentGroup { get; set; } = "全部";
    public bool ControlMsPpt { get; set; } = true;
    public bool ControlWpsPpt { get; set; } = true;
    public string WpsInputMode { get; set; } = WpsInputModeDefaults.Auto;
    public bool WpsWheelForward { get; set; } = true;
    public bool ForcePresentationForegroundOnFullscreen { get; set; } = false;
    public int WpsDebounceMs { get; set; } = 200;
    public bool PresentationLockStrategyWhenDegraded { get; set; } = true;
    public int LauncherX { get; set; } = 120;
    public int LauncherY { get; set; } = 120;
    public int LauncherBubbleX { get; set; } = 120;
    public int LauncherBubbleY { get; set; } = 120;
    public bool LauncherMinimized { get; set; } = false;
    public int LauncherAutoExitSeconds { get; set; } = 2400;
    public int PaintToolbarX { get; set; } = 260;
    public int PaintToolbarY { get; set; } = 260;
    public double PaintToolbarScale { get; set; } = 1.0;
    public bool InkCacheEnabled { get; set; } = true;
    public bool InkSaveEnabled { get; set; } = false;
    public InkExportScope InkExportScope { get; set; } = InkExportScope.AllPersistedAndSession;
    public int InkExportMaxParallelFiles { get; set; } = 2;
    public bool InkRecordEnabled { get; set; } = false;
    public bool InkReplayPreviousEnabled { get; set; } = false;
    public int InkRetentionDays { get; set; } = 30;
    public string InkPhotoRootPath { get; set; } = @"D:\ClassroomToolkit\Ink\Photos";
    public List<string> PhotoFavoriteFolders { get; set; } = new();
    public List<string> PhotoRecentFolders { get; set; } = new();
    public bool PhotoRememberTransform { get; set; } = false;
    public bool PhotoCrossPageDisplay { get; set; } = false;
    public bool PhotoInputTelemetryEnabled { get; set; } = false;
    public int PhotoNeighborPrefetchRadiusMax { get; set; } = 4;
    public int PhotoPostInputRefreshDelayMs { get; set; } = 120;
    public double PhotoWheelZoomBase { get; set; } = 1.0008;
    public double PhotoGestureZoomSensitivity { get; set; } = 1.0;
    public bool PhotoShowInkOverlay { get; set; } = true;
    public int PhotoManagerWindowWidth { get; set; } = 0;
    public int PhotoManagerWindowHeight { get; set; } = 0;
    public double PhotoManagerLeftPanelRatio { get; set; } = 2.0 / 7.0;
    public bool PhotoUnifiedTransformEnabled { get; set; } = false;
    public double PhotoUnifiedScaleX { get; set; } = 1.0;
    public double PhotoUnifiedScaleY { get; set; } = 1.0;
    public double PhotoUnifiedTranslateX { get; set; } = 0.0;
    public double PhotoUnifiedTranslateY { get; set; } = 0.0;

    public double BrushSize { get; set; } = 12;
    public double EraserSize { get; set; } = 24;
    public byte BrushOpacity { get; set; } = 255;
    public byte BoardOpacity { get; set; } = 255;
    public PaintBrushStyle BrushStyle { get; set; } = PaintBrushStyle.StandardRibbon;
    public WhiteboardBrushPreset WhiteboardPreset { get; set; } = WhiteboardBrushPreset.Smooth;
    public CalligraphyBrushPreset CalligraphyPreset { get; set; } = CalligraphyBrushPreset.Sharp;
    public string PresetScheme { get; set; } = "custom";
    public ClassroomWritingMode ClassroomWritingMode { get; set; } = ClassroomWritingMode.Balanced;
    public int StylusAdaptivePressureProfile { get; set; } = 0;
    public int StylusAdaptiveSampleRateTier { get; set; } = 0;
    public int StylusAdaptivePredictionHorizonMs { get; set; } = 8;
    public double StylusPressureCalibratedLow { get; set; } = 0.0;
    public double StylusPressureCalibratedHigh { get; set; } = 1.0;
    public bool CalligraphyInkBloomEnabled { get; set; } = true;
    public bool CalligraphySealEnabled { get; set; } = true;
    public byte CalligraphyOverlayOpacityThreshold { get; set; } = 230;
    public MediaColor BrushColor { get; set; } = MediaColors.Red;
    public MediaColor BoardColor { get; set; } = MediaColors.White;
    public MediaColor QuickColor1 { get; set; } = MediaColors.Black;
    public MediaColor QuickColor2 { get; set; } = MediaColors.Red;
    public MediaColor QuickColor3 { get; set; } = MediaColors.DodgerBlue;
    public PaintShapeType ShapeType { get; set; } = PaintShapeType.None;

    public string BrushColorHex => ToHex(BrushColor);
    public string BoardColorHex => ToHex(BoardColor);
    public string QuickColor1Hex => ToHex(QuickColor1);
    public string QuickColor2Hex => ToHex(QuickColor2);
    public string QuickColor3Hex => ToHex(QuickColor3);

    public static MediaColor ParseColor(string value, MediaColor fallback)
    {
        try
        {
            var parsed = (MediaColor)MediaColorConverter.ConvertFromString(value);
            return parsed;
        }
        catch (FormatException)
        {
            return fallback;
        }
        catch (NotSupportedException)
        {
            return fallback;
        }
    }

    private static string ToHex(MediaColor color)
    {
        return string.Create(CultureInfo.InvariantCulture, $"#{color.R:X2}{color.G:X2}{color.B:X2}");
    }
}
