using System.Collections.Generic;
using System.Globalization;

namespace ClassroomToolkit.App.Settings;

public sealed partial class AppSettingsService
{
    private static void ApplyRollCallSettings(Dictionary<string, string> roll, AppSettings settings)
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
        settings.RollCallSpeechEnabled = GetBool(roll, "speech_enabled", settings.RollCallSpeechEnabled);
        settings.RollCallSpeechEngine = ResolveSpeechEngine(
            GetString(roll, "speech_engine", settings.RollCallSpeechEngine),
            settings.RollCallSpeechEngine);
        settings.RollCallSpeechVoiceId = GetString(roll, "speech_voice_id", settings.RollCallSpeechVoiceId);
        settings.RollCallSpeechOutputId = GetString(roll, "speech_output_id", settings.RollCallSpeechOutputId);
        settings.RemotePresenterKey = GetString(roll, "remote_roll_key", settings.RemotePresenterKey);
        settings.RollCallRemoteGroupSwitchEnabled = GetBool(roll, "remote_group_switch_enabled", settings.RollCallRemoteGroupSwitchEnabled);
        settings.RemoteGroupSwitchKey = GetString(roll, "remote_group_switch_key", settings.RemoteGroupSwitchKey);
        settings.RollCallCurrentClass = GetString(roll, "current_class", settings.RollCallCurrentClass);
        settings.RollCallCurrentGroup = GetString(roll, "current_group", settings.RollCallCurrentGroup);
        var geometry = GetString(roll, "geometry", string.Empty);
        if (TryParseGeometry(geometry, out var width, out var height, out var x, out var y))
        {
            settings.RollCallWindowWidth = width;
            settings.RollCallWindowHeight = height;
            settings.RollCallWindowX = x;
            settings.RollCallWindowY = y;
        }
    }

    private static void ApplyLauncherSettings(Dictionary<string, string> launcher, AppSettings settings)
    {
        settings.LauncherX = GetInt(launcher, "x", settings.LauncherX);
        settings.LauncherY = GetInt(launcher, "y", settings.LauncherY);
        settings.LauncherBubbleX = GetInt(launcher, "bubble_x", settings.LauncherBubbleX);
        settings.LauncherBubbleY = GetInt(launcher, "bubble_y", settings.LauncherBubbleY);
        settings.LauncherMinimized = GetBool(launcher, "minimized", settings.LauncherMinimized);
        settings.LauncherAutoExitSeconds = GetInt(launcher, "auto_exit_seconds", settings.LauncherAutoExitSeconds);
        settings.LauncherAutoExitSeconds = NormalizeLauncherAutoExitSeconds(settings.LauncherAutoExitSeconds);
        var legacyUiDefaultsOptimized = GetBool(launcher, "ui_defaults_optimized", fallback: false);
        settings.UiDefaultsVersion = GetInt(
            launcher,
            "ui_defaults_version",
            legacyUiDefaultsOptimized ? UiDefaultsBootstrapOptimizationPolicy.CurrentVersion : settings.UiDefaultsVersion);
    }

    private static void ApplyDiagnosticsSettings(Dictionary<string, string> diagnostics, AppSettings settings)
    {
        settings.StartupCompatibilitySuppressedIssueCodes = ParseList(
            GetString(
                diagnostics,
                "startup_compatibility_suppressed_issue_codes",
                string.Empty));
    }

    private static void ApplyUiSettings(Dictionary<string, string> ui, AppSettings settings)
    {
        settings.UiTheme = UI.Themes.ThemePreferenceService.Normalize(
            GetString(ui, "theme", settings.UiTheme));
    }

    private static void ApplyUpdateSettings(Dictionary<string, string> update, AppSettings settings)
    {
        settings.UpdateAutoCheckEnabled = GetBool(update, "auto_check_enabled", settings.UpdateAutoCheckEnabled);
    }

    private static void SaveRollCallSettings(
        Dictionary<string, Dictionary<string, string>> data,
        AppSettings settings)
    {
        var roll = GetOrCreate(data, "RollCallTimer");
        SetBool(roll, "show_id", settings.RollCallShowId);
        SetBool(roll, "show_name", settings.RollCallShowName);
        SetBool(roll, "remote_roll_enabled", settings.RollCallRemoteEnabled);
        SetBool(roll, "show_photo", settings.RollCallShowPhoto);
        roll["photo_duration_seconds"] = settings.RollCallPhotoDurationSeconds.ToString(CultureInfo.InvariantCulture);
        roll["photo_shared_class"] = settings.RollCallPhotoSharedClass ?? string.Empty;
        SetBool(roll, "timer_sound_enabled", settings.RollCallTimerSoundEnabled);
        roll["timer_sound_variant"] = settings.RollCallTimerSoundVariant ?? "gentle";
        SetBool(roll, "timer_reminder_enabled", settings.RollCallTimerReminderEnabled);
        roll["timer_reminder_interval_minutes"] = settings.RollCallTimerReminderIntervalMinutes.ToString(CultureInfo.InvariantCulture);
        roll["timer_reminder_sound_variant"] = settings.RollCallTimerReminderSoundVariant ?? "soft_beep";
        roll["mode"] = settings.RollCallMode ?? "roll_call";
        roll["timer_mode"] = settings.RollCallTimerMode ?? "countdown";
        roll["timer_countdown_minutes"] = settings.RollCallTimerMinutes.ToString(CultureInfo.InvariantCulture);
        roll["timer_countdown_seconds"] = settings.RollCallTimerSeconds.ToString(CultureInfo.InvariantCulture);
        roll["timer_seconds_left"] = settings.RollCallTimerSecondsLeft.ToString(CultureInfo.InvariantCulture);
        roll["timer_stopwatch_seconds"] = settings.RollCallStopwatchSeconds.ToString(CultureInfo.InvariantCulture);
        RemoveLegacyRollCallKeys(data);
        SetBool(roll, "speech_enabled", settings.RollCallSpeechEnabled);
        roll["speech_engine"] = ResolveSpeechEngine(settings.RollCallSpeechEngine, "sapi");
        roll["speech_voice_id"] = settings.RollCallSpeechVoiceId ?? string.Empty;
        roll["speech_output_id"] = settings.RollCallSpeechOutputId ?? string.Empty;
        roll["remote_roll_key"] = settings.RemotePresenterKey;
        SetBool(roll, "remote_group_switch_enabled", settings.RollCallRemoteGroupSwitchEnabled);
        roll["remote_group_switch_key"] = settings.RemoteGroupSwitchKey;
        roll["current_class"] = settings.RollCallCurrentClass ?? string.Empty;
        roll["current_group"] = settings.RollCallCurrentGroup ?? "全部";
        if (HasGeometry(settings))
        {
            roll["geometry"] = FormatGeometry(
                settings.RollCallWindowWidth,
                settings.RollCallWindowHeight,
                settings.RollCallWindowX,
                settings.RollCallWindowY);
        }
    }

    private static void RemoveLegacyRollCallKeys(
        Dictionary<string, Dictionary<string, string>> data)
    {
        foreach (var sectionName in new[] { "RollCallTimer", "RollCall" })
        {
            if (!data.TryGetValue(sectionName, out var section) || section == null)
            {
                continue;
            }

            section.Remove("timer_running");
            section.Remove("id_font_size");
            section.Remove("name_font_size");
            section.Remove("timer_font_size");
        }
    }

    private static void SaveLauncherSettings(
        Dictionary<string, Dictionary<string, string>> data,
        AppSettings settings)
    {
        var launcher = GetOrCreate(data, "Launcher");
        launcher["x"] = settings.LauncherX.ToString(CultureInfo.InvariantCulture);
        launcher["y"] = settings.LauncherY.ToString(CultureInfo.InvariantCulture);
        launcher["bubble_x"] = settings.LauncherBubbleX.ToString(CultureInfo.InvariantCulture);
        launcher["bubble_y"] = settings.LauncherBubbleY.ToString(CultureInfo.InvariantCulture);
        SetBool(launcher, "minimized", settings.LauncherMinimized);
        launcher["auto_exit_seconds"] = NormalizeLauncherAutoExitSeconds(settings.LauncherAutoExitSeconds).ToString(CultureInfo.InvariantCulture);
        launcher["ui_defaults_version"] = settings.UiDefaultsVersion.ToString(CultureInfo.InvariantCulture);
        launcher.Remove("ui_defaults_optimized");
    }

    private static void SaveDiagnosticsSettings(
        Dictionary<string, Dictionary<string, string>> data,
        AppSettings settings)
    {
        var diagnostics = GetOrCreate(data, "Diagnostics");
        diagnostics["startup_compatibility_suppressed_issue_codes"] =
            JoinList(settings.StartupCompatibilitySuppressedIssueCodes);
    }

    private static void SaveUiSettings(
        Dictionary<string, Dictionary<string, string>> data,
        AppSettings settings)
    {
        var ui = GetOrCreate(data, "UI");
        ui["theme"] = UI.Themes.ThemePreferenceService.Normalize(settings.UiTheme);
    }

    private static void SaveUpdateSettings(
        Dictionary<string, Dictionary<string, string>> data,
        AppSettings settings)
    {
        var update = GetOrCreate(data, "Update");
        SetBool(update, "auto_check_enabled", settings.UpdateAutoCheckEnabled);
    }
}
