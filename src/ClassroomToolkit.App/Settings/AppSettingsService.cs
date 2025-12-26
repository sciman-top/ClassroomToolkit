using System.Globalization;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Infra.Settings;

namespace ClassroomToolkit.App.Settings;

public sealed class AppSettingsService
{
    private readonly SettingsRepository _repository;

    public AppSettingsService(string settingsPath)
    {
        _repository = new SettingsRepository(settingsPath);
    }

    public AppSettings Load()
    {
        var data = _repository.Load();
        var settings = new AppSettings();

        if (data.TryGetValue("RollCallTimer", out var roll))
        {
            settings.RollCallShowId = GetBool(roll, "show_id", settings.RollCallShowId);
            settings.RollCallShowName = GetBool(roll, "show_name", settings.RollCallShowName);
            settings.RollCallRemoteEnabled = GetBool(roll, "remote_roll_enabled", settings.RollCallRemoteEnabled);
            settings.RollCallShowPhoto = GetBool(roll, "show_photo", settings.RollCallShowPhoto);
            settings.RollCallPhotoDurationSeconds = GetInt(roll, "photo_duration_seconds", settings.RollCallPhotoDurationSeconds);
            settings.RollCallPhotoSharedClass = GetString(roll, "photo_shared_class", settings.RollCallPhotoSharedClass);
            settings.RollCallTimerSoundEnabled = GetBool(roll, "timer_sound_enabled", settings.RollCallTimerSoundEnabled);
            settings.RollCallTimerSoundVariant = GetString(roll, "timer_sound_variant", settings.RollCallTimerSoundVariant);
            settings.RollCallTimerReminderEnabled = GetBool(roll, "timer_reminder_enabled", settings.RollCallTimerReminderEnabled);
            settings.RollCallTimerReminderIntervalMinutes = GetInt(roll, "timer_reminder_interval_minutes", settings.RollCallTimerReminderIntervalMinutes);
            settings.RollCallTimerReminderSoundVariant = GetString(roll, "timer_reminder_sound_variant", settings.RollCallTimerReminderSoundVariant);
            settings.RollCallMode = GetString(roll, "mode", settings.RollCallMode);
            settings.RollCallTimerMode = GetString(roll, "timer_mode", settings.RollCallTimerMode);
            settings.RollCallTimerMinutes = GetInt(roll, "timer_countdown_minutes", settings.RollCallTimerMinutes);
            settings.RollCallTimerSeconds = GetInt(roll, "timer_countdown_seconds", settings.RollCallTimerSeconds);
            settings.RollCallTimerSecondsLeft = GetInt(roll, "timer_seconds_left", settings.RollCallTimerSecondsLeft);
            settings.RollCallStopwatchSeconds = GetInt(roll, "timer_stopwatch_seconds", settings.RollCallStopwatchSeconds);
            settings.RollCallTimerRunning = GetBool(roll, "timer_running", settings.RollCallTimerRunning);
            settings.RollCallSpeechEnabled = GetBool(roll, "speech_enabled", settings.RollCallSpeechEnabled);
            settings.RollCallSpeechEngine = GetString(roll, "speech_engine", settings.RollCallSpeechEngine);
            settings.RollCallSpeechVoiceId = GetString(roll, "speech_voice_id", settings.RollCallSpeechVoiceId);
            settings.RollCallSpeechOutputId = GetString(roll, "speech_output_id", settings.RollCallSpeechOutputId);
            settings.RemotePresenterKey = GetString(roll, "remote_roll_key", settings.RemotePresenterKey);
            settings.RollCallCurrentClass = GetString(roll, "current_class", settings.RollCallCurrentClass);
        }
        if (data.TryGetValue("Paint", out var paint))
        {
            settings.BrushSize = GetDouble(paint, "brush_base_size", settings.BrushSize);
            settings.EraserSize = GetDouble(paint, "eraser_size", settings.EraserSize);
            settings.BrushOpacity = GetByte(paint, "brush_opacity", settings.BrushOpacity);
            settings.BoardOpacity = GetByte(paint, "board_opacity", settings.BoardOpacity);
            settings.BrushColor = AppSettings.ParseColor(GetString(paint, "brush_color", settings.BrushColorHex), settings.BrushColor);
            settings.BoardColor = AppSettings.ParseColor(GetString(paint, "board_color", settings.BoardColorHex), settings.BoardColor);
            settings.ControlMsPpt = GetBool(paint, "control_ms_ppt", settings.ControlMsPpt);
            settings.ControlWpsPpt = GetBool(paint, "control_wps_ppt", settings.ControlWpsPpt);
            settings.WpsInputMode = GetString(paint, "wps_input_mode", settings.WpsInputMode);
            settings.WpsWheelForward = GetBool(paint, "wps_wheel_forward", settings.WpsWheelForward);
            settings.ShapeType = GetShapeType(GetString(paint, "shape_type", settings.ShapeType.ToString()));
        }
        if (data.TryGetValue("Launcher", out var launcher))
        {
            settings.LauncherX = GetInt(launcher, "x", settings.LauncherX);
            settings.LauncherY = GetInt(launcher, "y", settings.LauncherY);
            settings.LauncherBubbleX = GetInt(launcher, "bubble_x", settings.LauncherBubbleX);
            settings.LauncherBubbleY = GetInt(launcher, "bubble_y", settings.LauncherBubbleY);
            settings.LauncherMinimized = GetBool(launcher, "minimized", settings.LauncherMinimized);
            settings.LauncherAutoExitSeconds = GetInt(launcher, "auto_exit_seconds", settings.LauncherAutoExitSeconds);
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        var data = _repository.Load();
        var roll = GetOrCreate(data, "RollCallTimer");
        roll["show_id"] = settings.RollCallShowId ? "True" : "False";
        roll["show_name"] = settings.RollCallShowName ? "True" : "False";
        roll["remote_roll_enabled"] = settings.RollCallRemoteEnabled ? "True" : "False";
        roll["show_photo"] = settings.RollCallShowPhoto ? "True" : "False";
        roll["photo_duration_seconds"] = settings.RollCallPhotoDurationSeconds.ToString(CultureInfo.InvariantCulture);
        roll["photo_shared_class"] = settings.RollCallPhotoSharedClass ?? string.Empty;
        roll["timer_sound_enabled"] = settings.RollCallTimerSoundEnabled ? "True" : "False";
        roll["timer_sound_variant"] = settings.RollCallTimerSoundVariant ?? "gentle";
        roll["timer_reminder_enabled"] = settings.RollCallTimerReminderEnabled ? "True" : "False";
        roll["timer_reminder_interval_minutes"] = settings.RollCallTimerReminderIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        roll["timer_reminder_sound_variant"] = settings.RollCallTimerReminderSoundVariant ?? "soft_beep";
        roll["mode"] = settings.RollCallMode ?? "roll_call";
        roll["timer_mode"] = settings.RollCallTimerMode ?? "countdown";
        roll["timer_countdown_minutes"] = settings.RollCallTimerMinutes.ToString(CultureInfo.InvariantCulture);
        roll["timer_countdown_seconds"] = settings.RollCallTimerSeconds.ToString(CultureInfo.InvariantCulture);
        roll["timer_seconds_left"] = settings.RollCallTimerSecondsLeft.ToString(CultureInfo.InvariantCulture);
        roll["timer_stopwatch_seconds"] = settings.RollCallStopwatchSeconds.ToString(CultureInfo.InvariantCulture);
        roll["timer_running"] = settings.RollCallTimerRunning ? "True" : "False";
        roll["speech_enabled"] = settings.RollCallSpeechEnabled ? "True" : "False";
        roll["speech_engine"] = settings.RollCallSpeechEngine ?? "pyttsx3";
        roll["speech_voice_id"] = settings.RollCallSpeechVoiceId ?? string.Empty;
        roll["speech_output_id"] = settings.RollCallSpeechOutputId ?? string.Empty;
        roll["remote_roll_key"] = settings.RemotePresenterKey;
        roll["current_class"] = settings.RollCallCurrentClass ?? string.Empty;

        var paint = GetOrCreate(data, "Paint");
        paint["brush_base_size"] = settings.BrushSize.ToString("0.##", CultureInfo.InvariantCulture);
        paint["eraser_size"] = settings.EraserSize.ToString("0.##", CultureInfo.InvariantCulture);
        paint["brush_opacity"] = settings.BrushOpacity.ToString(CultureInfo.InvariantCulture);
        paint["board_opacity"] = settings.BoardOpacity.ToString(CultureInfo.InvariantCulture);
        paint["brush_color"] = settings.BrushColorHex;
        paint["board_color"] = settings.BoardColorHex;
        paint["control_ms_ppt"] = settings.ControlMsPpt ? "True" : "False";
        paint["control_wps_ppt"] = settings.ControlWpsPpt ? "True" : "False";
        paint["wps_input_mode"] = settings.WpsInputMode;
        paint["wps_wheel_forward"] = settings.WpsWheelForward ? "True" : "False";
        paint["shape_type"] = settings.ShapeType.ToString();

        var launcher = GetOrCreate(data, "Launcher");
        launcher["x"] = settings.LauncherX.ToString(CultureInfo.InvariantCulture);
        launcher["y"] = settings.LauncherY.ToString(CultureInfo.InvariantCulture);
        launcher["bubble_x"] = settings.LauncherBubbleX.ToString(CultureInfo.InvariantCulture);
        launcher["bubble_y"] = settings.LauncherBubbleY.ToString(CultureInfo.InvariantCulture);
        launcher["minimized"] = settings.LauncherMinimized ? "True" : "False";
        launcher["auto_exit_seconds"] = settings.LauncherAutoExitSeconds.ToString(CultureInfo.InvariantCulture);

        _repository.Save(data);
    }

    private static Dictionary<string, string> GetOrCreate(Dictionary<string, Dictionary<string, string>> data, string key)
    {
        if (!data.TryGetValue(key, out var section))
        {
            section = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            data[key] = section;
        }
        return section;
    }

    private static string GetString(Dictionary<string, string> section, string key, string fallback)
    {
        return section.TryGetValue(key, out var value) ? value : fallback;
    }

    private static double GetDouble(Dictionary<string, string> section, string key, double fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private static byte GetByte(Dictionary<string, string> section, string key, byte fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return byte.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private static int GetInt(Dictionary<string, string> section, string key, int fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result) ? result : fallback;
    }

    private static bool GetBool(Dictionary<string, string> section, string key, bool fallback)
    {
        if (!section.TryGetValue(key, out var value))
        {
            return fallback;
        }
        return value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ||
               value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static PaintShapeType GetShapeType(string raw)
    {
        if (Enum.TryParse<PaintShapeType>(raw, true, out var parsed))
        {
            return parsed;
        }
        return PaintShapeType.Line;
    }
}
