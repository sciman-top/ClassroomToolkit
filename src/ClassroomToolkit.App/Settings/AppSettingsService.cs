using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;

namespace ClassroomToolkit.App.Settings;

public sealed class AppSettingsService
{
    private static readonly Regex GeometryRegex = new(
        @"^(?<w>\d+)x(?<h>\d+)(?<x>[+-]\d+)(?<y>[+-]\d+)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ISettingsDocumentStore _store;

    public AppSettingsService(ISettingsDocumentStore store)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
    }

    public AppSettings Load()
    {
        var data = _store.Load();
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
            settings.RollCallIdFontSize = GetInt(roll, "id_font_size", settings.RollCallIdFontSize);
            settings.RollCallNameFontSize = GetInt(roll, "name_font_size", settings.RollCallNameFontSize);
            settings.RollCallTimerFontSize = GetInt(roll, "timer_font_size", settings.RollCallTimerFontSize);
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
        if (data.TryGetValue("Paint", out var paint))
        {
            settings.BrushSize = GetDouble(paint, "brush_base_size", settings.BrushSize);
            settings.EraserSize = GetDouble(paint, "eraser_size", settings.EraserSize);
            settings.BrushOpacity = GetByte(paint, "brush_opacity", settings.BrushOpacity);
            settings.BrushStyle = GetBrushStyle(GetString(paint, "brush_style", settings.BrushStyle.ToString()));
            settings.WhiteboardPreset = ResolveWhiteboardPreset(paint, settings.WhiteboardPreset);
            settings.CalligraphyPreset = ResolveCalligraphyPreset(paint, settings.CalligraphyPreset);
            settings.PresetScheme = NormalizePresetScheme(
                GetString(paint, "preset_scheme", settings.PresetScheme));
            settings.ClassroomWritingMode = ResolveClassroomWritingMode(paint, settings.ClassroomWritingMode);
            settings.StylusAdaptivePressureProfile = NormalizeStylusPressureProfile(
                GetInt(
                    paint,
                    "stylus_adaptive_pressure_profile",
                    settings.StylusAdaptivePressureProfile));
            settings.StylusAdaptiveSampleRateTier = NormalizeStylusSampleRateTier(
                GetInt(
                    paint,
                    "stylus_adaptive_sample_rate_tier",
                    settings.StylusAdaptiveSampleRateTier));
            settings.StylusAdaptivePredictionHorizonMs = NormalizeStylusPredictionHorizonMs(
                GetInt(
                    paint,
                    "stylus_adaptive_prediction_horizon_ms",
                    settings.StylusAdaptivePredictionHorizonMs));
            var (normalizedLow, normalizedHigh) = NormalizeStylusCalibrationRange(
                GetDouble(
                    paint,
                    "stylus_pressure_calibrated_low",
                    settings.StylusPressureCalibratedLow),
                GetDouble(
                    paint,
                    "stylus_pressure_calibrated_high",
                    settings.StylusPressureCalibratedHigh));
            settings.StylusPressureCalibratedLow = normalizedLow;
            settings.StylusPressureCalibratedHigh = normalizedHigh;
            settings.CalligraphyInkBloomEnabled = GetBool(
                paint,
                "calligraphy_ink_bloom_enabled",
                settings.CalligraphyInkBloomEnabled);
            settings.CalligraphySealEnabled = GetBool(
                paint,
                "calligraphy_seal_enabled",
                settings.CalligraphySealEnabled);
            settings.CalligraphyOverlayOpacityThreshold = GetByte(
                paint,
                "calligraphy_overlay_opacity_threshold",
                settings.CalligraphyOverlayOpacityThreshold);
            settings.BoardOpacity = 255;
            settings.BrushColor = AppSettings.ParseColor(GetString(paint, "brush_color", settings.BrushColorHex), settings.BrushColor);
            settings.BoardColor = AppSettings.ParseColor(GetString(paint, "board_color", settings.BoardColorHex), settings.BoardColor);
            settings.QuickColor1 = AppSettings.ParseColor(GetString(paint, "quick_color_1", settings.QuickColor1Hex), settings.QuickColor1);
            settings.QuickColor2 = AppSettings.ParseColor(GetString(paint, "quick_color_2", settings.QuickColor2Hex), settings.QuickColor2);
            settings.QuickColor3 = AppSettings.ParseColor(GetString(paint, "quick_color_3", settings.QuickColor3Hex), settings.QuickColor3);
            settings.ControlMsPpt = GetBool(paint, "control_ms_ppt", settings.ControlMsPpt);
            settings.ControlWpsPpt = GetBool(paint, "control_wps_ppt", settings.ControlWpsPpt);
            settings.WpsInputMode = ResolveWpsInputMode(paint, settings.WpsInputMode);
            settings.WpsWheelForward = GetBool(paint, "wps_wheel_forward", settings.WpsWheelForward);
            settings.ForcePresentationForegroundOnFullscreen = GetBool(
                paint,
                "force_presentation_foreground_on_fullscreen",
                settings.ForcePresentationForegroundOnFullscreen);
            settings.WpsDebounceMs = GetInt(paint, "wps_debounce_ms", settings.WpsDebounceMs);
            settings.WpsDebounceMs = NormalizeWpsDebounceMs(settings.WpsDebounceMs);
            settings.PresentationLockStrategyWhenDegraded = GetBool(
                paint,
                "presentation_lock_strategy_when_degraded",
                settings.PresentationLockStrategyWhenDegraded);
            settings.PresetRecommendationInitialized = GetBool(
                paint,
                "preset_recommendation_initialized",
                settings.PresetRecommendationInitialized);
            settings.ShapeType = GetShapeType(GetString(paint, "shape_type", settings.ShapeType.ToString()));
            settings.PaintToolbarX = GetInt(paint, "x", settings.PaintToolbarX);
            settings.PaintToolbarY = GetInt(paint, "y", settings.PaintToolbarY);
            settings.PaintToolbarScale = GetDouble(paint, "toolbar_scale", settings.PaintToolbarScale);
            settings.PaintToolbarScale = NormalizePaintToolbarScale(settings.PaintToolbarScale);
            settings.InkCacheEnabled = GetBool(paint, "ink_cache_enabled", settings.InkCacheEnabled);
            settings.InkSaveEnabled = GetBool(paint, "ink_save_enabled", settings.InkSaveEnabled);
            settings.InkExportScope = GetInkExportScope(GetString(paint, "ink_export_scope", settings.InkExportScope.ToString()));
            settings.InkExportMaxParallelFiles = GetInt(paint, "ink_export_max_parallel_files", settings.InkExportMaxParallelFiles);
            settings.InkExportMaxParallelFiles = NormalizeInkExportMaxParallelFiles(settings.InkExportMaxParallelFiles);
            settings.InkRecordEnabled = GetBool(paint, "ink_record_enabled", settings.InkRecordEnabled);
            settings.InkReplayPreviousEnabled = GetBool(paint, "ink_replay_previous_enabled", settings.InkReplayPreviousEnabled);
            settings.InkRetentionDays = GetInt(paint, "ink_retention_days", settings.InkRetentionDays);
            settings.InkRetentionDays = NormalizeInkRetentionDays(settings.InkRetentionDays);
            settings.InkPhotoRootPath = NormalizeInkPhotoRootPath(
                GetString(paint, "ink_photo_root_path", settings.InkPhotoRootPath));
            settings.PhotoFavoriteFolders = ParseList(GetString(paint, "photo_favorites", string.Empty));
            settings.PhotoRecentFolders = ParseList(GetString(paint, "photo_recent_folders", string.Empty));
            settings.PhotoRememberTransform = GetBool(paint, "photo_remember_transform", settings.PhotoRememberTransform);
            settings.PhotoCrossPageDisplay = GetBool(paint, "photo_cross_page_display", settings.PhotoCrossPageDisplay);
            settings.PhotoInputTelemetryEnabled = GetBool(paint, "photo_input_telemetry_enabled", settings.PhotoInputTelemetryEnabled);
            settings.PhotoNeighborPrefetchRadiusMax = GetInt(paint, "photo_neighbor_prefetch_radius_max", settings.PhotoNeighborPrefetchRadiusMax);
            settings.PhotoNeighborPrefetchRadiusMax = NormalizePhotoNeighborPrefetchRadiusMax(settings.PhotoNeighborPrefetchRadiusMax);
            settings.PhotoPostInputRefreshDelayMs = GetInt(
                paint,
                "photo_post_input_refresh_delay_ms",
                settings.PhotoPostInputRefreshDelayMs);
            settings.PhotoPostInputRefreshDelayMs = NormalizePhotoPostInputRefreshDelayMs(settings.PhotoPostInputRefreshDelayMs);
            settings.PhotoWheelZoomBase = NormalizePhotoWheelZoomBase(
                GetDouble(paint, "photo_wheel_zoom_base", settings.PhotoWheelZoomBase));
            settings.PhotoGestureZoomSensitivity = NormalizePhotoGestureZoomSensitivity(
                GetDouble(
                    paint,
                    "photo_gesture_zoom_sensitivity",
                    settings.PhotoGestureZoomSensitivity));
            settings.PhotoShowInkOverlay = GetBool(paint, "photo_show_ink_overlay", settings.PhotoShowInkOverlay);
            settings.PhotoManagerWindowWidth = GetInt(paint, "photo_manager_window_width", settings.PhotoManagerWindowWidth);
            settings.PhotoManagerWindowHeight = GetInt(paint, "photo_manager_window_height", settings.PhotoManagerWindowHeight);
            settings.PhotoManagerLeftPanelRatio = GetDouble(paint, "photo_manager_left_panel_ratio", settings.PhotoManagerLeftPanelRatio);
            settings.PhotoManagerLeftPanelWidth = GetInt(paint, "photo_manager_left_panel_width", settings.PhotoManagerLeftPanelWidth);
            settings.PhotoManagerThumbnailSize = GetDouble(
                paint,
                "photo_manager_thumbnail_size",
                settings.PhotoManagerThumbnailSize);
            settings.PhotoManagerListMode = GetBool(
                paint,
                "photo_manager_list_mode",
                settings.PhotoManagerListMode);
            settings.PhotoUnifiedTransformEnabled = GetBool(
                paint,
                "photo_unified_transform_enabled",
                settings.PhotoUnifiedTransformEnabled);
            settings.PhotoUnifiedScaleX = GetDouble(
                paint,
                "photo_unified_scale_x",
                settings.PhotoUnifiedScaleX);
            settings.PhotoUnifiedScaleY = GetDouble(
                paint,
                "photo_unified_scale_y",
                settings.PhotoUnifiedScaleY);
            settings.PhotoUnifiedTranslateX = GetDouble(
                paint,
                "photo_unified_translate_x",
                settings.PhotoUnifiedTranslateX);
            settings.PhotoUnifiedTranslateY = GetDouble(
                paint,
                "photo_unified_translate_y",
                settings.PhotoUnifiedTranslateY);
        }
        if (data.TryGetValue("Launcher", out var launcher))
        {
            settings.LauncherX = GetInt(launcher, "x", settings.LauncherX);
            settings.LauncherY = GetInt(launcher, "y", settings.LauncherY);
            settings.LauncherBubbleX = GetInt(launcher, "bubble_x", settings.LauncherBubbleX);
            settings.LauncherBubbleY = GetInt(launcher, "bubble_y", settings.LauncherBubbleY);
            settings.LauncherMinimized = GetBool(launcher, "minimized", settings.LauncherMinimized);
            settings.LauncherAutoExitSeconds = GetInt(launcher, "auto_exit_seconds", settings.LauncherAutoExitSeconds);
            settings.LauncherAutoExitSeconds = NormalizeLauncherAutoExitSeconds(settings.LauncherAutoExitSeconds);
            settings.UiDefaultsOptimized = GetBool(
                launcher,
                "ui_defaults_optimized",
                settings.UiDefaultsOptimized);
        }

        return settings;
    }

    public void Save(AppSettings settings)
    {
        var data = _store.Load();
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
        roll["id_font_size"] = settings.RollCallIdFontSize.ToString(CultureInfo.InvariantCulture);
        roll["name_font_size"] = settings.RollCallNameFontSize.ToString(CultureInfo.InvariantCulture);
        roll["timer_font_size"] = settings.RollCallTimerFontSize.ToString(CultureInfo.InvariantCulture);
        roll["speech_enabled"] = settings.RollCallSpeechEnabled ? "True" : "False";
        roll["speech_engine"] = ResolveSpeechEngine(settings.RollCallSpeechEngine, "sapi");
        roll["speech_voice_id"] = settings.RollCallSpeechVoiceId ?? string.Empty;
        roll["speech_output_id"] = settings.RollCallSpeechOutputId ?? string.Empty;
        roll["remote_roll_key"] = settings.RemotePresenterKey;
        roll["remote_group_switch_enabled"] = settings.RollCallRemoteGroupSwitchEnabled ? "True" : "False";
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

        var paint = GetOrCreate(data, "Paint");
        paint["brush_base_size"] = settings.BrushSize.ToString("0.##", CultureInfo.InvariantCulture);
        paint["brush_style"] = settings.BrushStyle.ToString();
        paint["whiteboard_preset"] = settings.WhiteboardPreset.ToString();
        paint["calligraphy_preset"] = settings.CalligraphyPreset.ToString();
        paint["preset_scheme"] = NormalizePresetScheme(settings.PresetScheme);
        paint["classroom_writing_mode"] = settings.ClassroomWritingMode.ToString();
        paint["stylus_adaptive_pressure_profile"] = NormalizeStylusPressureProfile(settings.StylusAdaptivePressureProfile).ToString(CultureInfo.InvariantCulture);
        paint["stylus_adaptive_sample_rate_tier"] = NormalizeStylusSampleRateTier(settings.StylusAdaptiveSampleRateTier).ToString(CultureInfo.InvariantCulture);
        paint["stylus_adaptive_prediction_horizon_ms"] = NormalizeStylusPredictionHorizonMs(settings.StylusAdaptivePredictionHorizonMs).ToString(CultureInfo.InvariantCulture);
        var (normalizedLow, normalizedHigh) = NormalizeStylusCalibrationRange(
            settings.StylusPressureCalibratedLow,
            settings.StylusPressureCalibratedHigh);
        paint["stylus_pressure_calibrated_low"] = normalizedLow.ToString("0.####", CultureInfo.InvariantCulture);
        paint["stylus_pressure_calibrated_high"] = normalizedHigh.ToString("0.####", CultureInfo.InvariantCulture);
        paint["calligraphy_ink_bloom_enabled"] = settings.CalligraphyInkBloomEnabled ? "True" : "False";
        paint["calligraphy_seal_enabled"] = settings.CalligraphySealEnabled ? "True" : "False";
        paint["calligraphy_overlay_opacity_threshold"] =
            settings.CalligraphyOverlayOpacityThreshold.ToString(CultureInfo.InvariantCulture);
        paint["eraser_size"] = settings.EraserSize.ToString("0.##", CultureInfo.InvariantCulture);
        paint["brush_opacity"] = settings.BrushOpacity.ToString(CultureInfo.InvariantCulture);
        paint["board_opacity"] = settings.BoardOpacity.ToString(CultureInfo.InvariantCulture);
        paint["brush_color"] = settings.BrushColorHex;
        paint["board_color"] = settings.BoardColorHex;
        paint["quick_color_1"] = settings.QuickColor1Hex;
        paint["quick_color_2"] = settings.QuickColor2Hex;
        paint["quick_color_3"] = settings.QuickColor3Hex;
        paint["control_ms_ppt"] = settings.ControlMsPpt ? "True" : "False";
        paint["control_wps_ppt"] = settings.ControlWpsPpt ? "True" : "False";
        paint["wps_input_mode"] = NormalizeWpsInputMode(settings.WpsInputMode, WpsInputModeDefaults.Auto);
        paint["wps_wheel_forward"] = settings.WpsWheelForward ? "True" : "False";
        paint["force_presentation_foreground_on_fullscreen"] =
            settings.ForcePresentationForegroundOnFullscreen ? "True" : "False";
        paint["wps_debounce_ms"] = NormalizeWpsDebounceMs(settings.WpsDebounceMs).ToString(CultureInfo.InvariantCulture);
        paint["presentation_lock_strategy_when_degraded"] =
            settings.PresentationLockStrategyWhenDegraded ? "True" : "False";
        paint["preset_recommendation_initialized"] =
            settings.PresetRecommendationInitialized ? "True" : "False";
        paint["shape_type"] = settings.ShapeType.ToString();
        paint["x"] = settings.PaintToolbarX.ToString(CultureInfo.InvariantCulture);
        paint["y"] = settings.PaintToolbarY.ToString(CultureInfo.InvariantCulture);
        paint["toolbar_scale"] = NormalizePaintToolbarScale(settings.PaintToolbarScale).ToString(CultureInfo.InvariantCulture);
        paint["ink_cache_enabled"] = settings.InkCacheEnabled ? "True" : "False";
        paint["ink_save_enabled"] = settings.InkSaveEnabled ? "True" : "False";
        paint["ink_export_scope"] = settings.InkExportScope.ToString();
        paint["ink_export_max_parallel_files"] = NormalizeInkExportMaxParallelFiles(settings.InkExportMaxParallelFiles).ToString(CultureInfo.InvariantCulture);
        paint["ink_record_enabled"] = settings.InkRecordEnabled ? "True" : "False";
        paint["ink_replay_previous_enabled"] = settings.InkReplayPreviousEnabled ? "True" : "False";
        paint["ink_retention_days"] = NormalizeInkRetentionDays(settings.InkRetentionDays).ToString(CultureInfo.InvariantCulture);
        paint["ink_photo_root_path"] = NormalizeInkPhotoRootPath(settings.InkPhotoRootPath);
        paint["photo_favorites"] = JoinList(settings.PhotoFavoriteFolders);
        paint["photo_recent_folders"] = JoinList(settings.PhotoRecentFolders);
        paint["photo_remember_transform"] = settings.PhotoRememberTransform ? "True" : "False";
        paint["photo_cross_page_display"] = settings.PhotoCrossPageDisplay ? "True" : "False";
        paint["photo_input_telemetry_enabled"] = settings.PhotoInputTelemetryEnabled ? "True" : "False";
        paint["photo_neighbor_prefetch_radius_max"] = NormalizePhotoNeighborPrefetchRadiusMax(settings.PhotoNeighborPrefetchRadiusMax).ToString(CultureInfo.InvariantCulture);
        paint["photo_post_input_refresh_delay_ms"] = NormalizePhotoPostInputRefreshDelayMs(settings.PhotoPostInputRefreshDelayMs).ToString(CultureInfo.InvariantCulture);
        paint["photo_wheel_zoom_base"] = NormalizePhotoWheelZoomBase(settings.PhotoWheelZoomBase).ToString("0.####", CultureInfo.InvariantCulture);
        paint["photo_gesture_zoom_sensitivity"] = NormalizePhotoGestureZoomSensitivity(settings.PhotoGestureZoomSensitivity).ToString("0.###", CultureInfo.InvariantCulture);
        paint["photo_show_ink_overlay"] = settings.PhotoShowInkOverlay ? "True" : "False";
        paint["photo_manager_window_width"] = settings.PhotoManagerWindowWidth.ToString(CultureInfo.InvariantCulture);
        paint["photo_manager_window_height"] = settings.PhotoManagerWindowHeight.ToString(CultureInfo.InvariantCulture);
        paint["photo_manager_left_panel_ratio"] = settings.PhotoManagerLeftPanelRatio.ToString("0.####", CultureInfo.InvariantCulture);
        paint["photo_manager_left_panel_width"] = settings.PhotoManagerLeftPanelWidth.ToString(CultureInfo.InvariantCulture);
        paint["photo_manager_thumbnail_size"] = settings.PhotoManagerThumbnailSize.ToString("0.##", CultureInfo.InvariantCulture);
        paint["photo_manager_list_mode"] = settings.PhotoManagerListMode ? "True" : "False";
        paint["photo_unified_transform_enabled"] = settings.PhotoUnifiedTransformEnabled ? "True" : "False";
        paint["photo_unified_scale_x"] = settings.PhotoUnifiedScaleX.ToString(CultureInfo.InvariantCulture);
        paint["photo_unified_scale_y"] = settings.PhotoUnifiedScaleY.ToString(CultureInfo.InvariantCulture);
        paint["photo_unified_translate_x"] = settings.PhotoUnifiedTranslateX.ToString(CultureInfo.InvariantCulture);
        paint["photo_unified_translate_y"] = settings.PhotoUnifiedTranslateY.ToString(CultureInfo.InvariantCulture);
        paint.Remove("ink_sidebar_x");
        paint.Remove("ink_sidebar_y");
        paint.Remove("ink_sidebar_enabled");
        paint.Remove("photo_current_page");
        paint.Remove("photo_current_index");
        paint.Remove("pdf_current_page");
        paint.Remove("image_current_index");
        paint.Remove("ink_auto_save_enabled");
        paint.Remove("ppt_current_slide");
        paint.Remove("wps_current_slide");
        paint.Remove("presentation_current_page");
        paint.Remove("wps_raw_input");

        var launcher = GetOrCreate(data, "Launcher");
        launcher["x"] = settings.LauncherX.ToString(CultureInfo.InvariantCulture);
        launcher["y"] = settings.LauncherY.ToString(CultureInfo.InvariantCulture);
        launcher["bubble_x"] = settings.LauncherBubbleX.ToString(CultureInfo.InvariantCulture);
        launcher["bubble_y"] = settings.LauncherBubbleY.ToString(CultureInfo.InvariantCulture);
        launcher["minimized"] = settings.LauncherMinimized ? "True" : "False";
        launcher["auto_exit_seconds"] = NormalizeLauncherAutoExitSeconds(settings.LauncherAutoExitSeconds).ToString(CultureInfo.InvariantCulture);
        launcher["ui_defaults_optimized"] = settings.UiDefaultsOptimized ? "True" : "False";

        _store.Save(data);
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
        var normalized = value.Trim();
        if (normalized.Equals("true", StringComparison.OrdinalIgnoreCase) || normalized.Equals("1", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.Equals("yes", StringComparison.OrdinalIgnoreCase) || normalized.Equals("on", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }
        if (normalized.Equals("false", StringComparison.OrdinalIgnoreCase) || normalized.Equals("0", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (normalized.Equals("no", StringComparison.OrdinalIgnoreCase) || normalized.Equals("off", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return fallback;
    }

    private static string ResolveSpeechEngine(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.IsNullOrWhiteSpace(fallback) ? "sapi" : fallback;
        }

        var normalized = value.Trim();
        if (normalized.Equals("pyttsx3", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("sapi", StringComparison.OrdinalIgnoreCase))
        {
            return "sapi";
        }

        return string.IsNullOrWhiteSpace(fallback) ? "sapi" : fallback;
    }

    private static PaintShapeType GetShapeType(string raw)
    {
        if (Enum.TryParse<PaintShapeType>(raw, true, out var parsed))
        {
            if (parsed == PaintShapeType.RectangleFill)
            {
                return PaintShapeType.Rectangle;
            }
            return parsed;
        }
        return PaintShapeType.None;
    }

    private static PaintBrushStyle GetBrushStyle(string raw)
    {
        if (Enum.TryParse<PaintBrushStyle>(raw, true, out var parsed))
        {
            if (parsed == PaintBrushStyle.Standard)
            {
                return PaintBrushStyle.StandardRibbon;
            }
            return parsed;
        }
        return PaintBrushStyle.StandardRibbon;
    }

    private static InkExportScope GetInkExportScope(string raw)
    {
        if (Enum.TryParse<InkExportScope>(raw, true, out var parsed))
        {
            return parsed;
        }
        return InkExportScope.AllPersistedAndSession;
    }

    private static WhiteboardBrushPreset ResolveWhiteboardPreset(Dictionary<string, string> section, WhiteboardBrushPreset fallback)
    {
        if (section.TryGetValue("whiteboard_preset", out var raw) &&
            Enum.TryParse<WhiteboardBrushPreset>(raw, true, out var parsed))
        {
            return parsed;
        }
        if (section.TryGetValue("whiteboard_preset", out var legacyPreset) &&
            legacyPreset.Trim().Equals("Compatibility", StringComparison.OrdinalIgnoreCase))
        {
            return WhiteboardBrushPreset.Sharp;
        }

        if (section.ContainsKey("whiteboard_smooth_mode"))
        {
            bool legacy = GetBool(section, "whiteboard_smooth_mode", true);
            return legacy ? WhiteboardBrushPreset.Smooth : WhiteboardBrushPreset.Sharp;
        }

        return fallback;
    }

    private static CalligraphyBrushPreset ResolveCalligraphyPreset(Dictionary<string, string> section, CalligraphyBrushPreset fallback)
    {
        if (section.TryGetValue("calligraphy_preset", out var raw) &&
            Enum.TryParse<CalligraphyBrushPreset>(raw, true, out var parsed))
        {
            return parsed;
        }

        if (section.ContainsKey("calligraphy_sharp_mode"))
        {
            bool legacy = GetBool(section, "calligraphy_sharp_mode", true);
            return legacy ? CalligraphyBrushPreset.Sharp : CalligraphyBrushPreset.Soft;
        }

        return fallback;
    }

    private static ClassroomWritingMode ResolveClassroomWritingMode(Dictionary<string, string> section, ClassroomWritingMode fallback)
    {
        if (section.TryGetValue("classroom_writing_mode", out var raw) &&
            Enum.TryParse<ClassroomWritingMode>(raw, true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string ResolveWpsInputMode(Dictionary<string, string> section, string fallback)
    {
        var mode = GetString(section, "wps_input_mode", fallback);
        var normalizedFallback = NormalizeWpsInputMode(fallback, WpsInputModeDefaults.Auto);
        if (mode.Trim().Equals("manual", StringComparison.OrdinalIgnoreCase))
        {
            var rawInput = GetBool(section, "wps_raw_input", fallback: false);
            return rawInput ? WpsInputModeDefaults.Raw : WpsInputModeDefaults.Message;
        }

        return NormalizeWpsInputMode(mode, normalizedFallback);
    }

    private static string NormalizeWpsInputMode(string? rawMode, string fallback)
    {
        var normalizedFallback = string.IsNullOrWhiteSpace(fallback)
            ? WpsInputModeDefaults.Auto
            : fallback.Trim().ToLowerInvariant();
        var normalized = (rawMode ?? string.Empty).Trim().ToLowerInvariant();
        return normalized switch
        {
            WpsInputModeDefaults.Auto => WpsInputModeDefaults.Auto,
            WpsInputModeDefaults.Raw => WpsInputModeDefaults.Raw,
            WpsInputModeDefaults.Message => WpsInputModeDefaults.Message,
            _ => normalizedFallback switch
            {
                WpsInputModeDefaults.Raw => WpsInputModeDefaults.Raw,
                WpsInputModeDefaults.Message => WpsInputModeDefaults.Message,
                _ => WpsInputModeDefaults.Auto
            }
        };
    }

    private static string NormalizePresetScheme(string? rawScheme)
    {
        var normalized = (rawScheme ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return PresetSchemeDefaults.Custom;
        }

        return normalized is PresetSchemeDefaults.Custom
            or PresetSchemeDefaults.Balanced
            or PresetSchemeDefaults.Responsive
            or PresetSchemeDefaults.Stable
            or PresetSchemeDefaults.DualScreen
            ? normalized
            : PresetSchemeDefaults.Custom;
    }

    private static int NormalizeStylusPressureProfile(int rawProfile)
    {
        return Enum.IsDefined(typeof(StylusPressureDeviceProfile), rawProfile)
            ? rawProfile
            : (int)StylusPressureDeviceProfile.Unknown;
    }

    private static int NormalizeStylusSampleRateTier(int rawTier)
    {
        return Enum.IsDefined(typeof(StylusSampleRateTier), rawTier)
            ? rawTier
            : (int)StylusSampleRateTier.Unknown;
    }

    private static int NormalizeStylusPredictionHorizonMs(int rawPredictionHorizonMs)
    {
        return Math.Clamp(
            rawPredictionHorizonMs,
            StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMinMs,
            StylusAdaptiveProfilingDefaults.SeedPredictionHorizonMaxMs);
    }

    private static int NormalizeWpsDebounceMs(int debounceMs)
    {
        return Math.Max(0, debounceMs);
    }

    private static double NormalizePaintToolbarScale(double scale)
    {
        return Math.Clamp(scale, ToolbarScaleDefaults.Min, ToolbarScaleDefaults.Max);
    }

    private static int NormalizeInkExportMaxParallelFiles(int maxParallelFiles)
    {
        return Math.Max(0, maxParallelFiles);
    }

    private static int NormalizeInkRetentionDays(int retentionDays)
    {
        return Math.Max(0, retentionDays);
    }

    private static string NormalizeInkPhotoRootPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return AppSettings.ResolveDefaultInkPhotoRootPath();
        }

        return path.Trim();
    }

    private static int NormalizePhotoNeighborPrefetchRadiusMax(int maxRadius)
    {
        return Math.Clamp(
            maxRadius,
            CrossPageNeighborPrefetchDefaults.RadiusMin,
            CrossPageNeighborPrefetchDefaults.RadiusMax);
    }

    private static int NormalizePhotoPostInputRefreshDelayMs(int delayMs)
    {
        return CrossPagePostInputRefreshDelayClampPolicy.Clamp(delayMs);
    }

    private static double NormalizePhotoWheelZoomBase(double wheelZoomBase)
    {
        return Math.Clamp(
            wheelZoomBase,
            PhotoZoomInputDefaults.WheelZoomBaseMin,
            PhotoZoomInputDefaults.WheelZoomBaseMax);
    }

    private static double NormalizePhotoGestureZoomSensitivity(double sensitivity)
    {
        return Math.Clamp(
            sensitivity,
            PhotoZoomInputDefaults.GestureSensitivityMin,
            PhotoZoomInputDefaults.GestureSensitivityMax);
    }

    private static int NormalizeLauncherAutoExitSeconds(int autoExitSeconds)
    {
        return Math.Max(0, autoExitSeconds);
    }

    private static double ClampUnitInterval(double value)
    {
        return Math.Clamp(value, 0.0, 1.0);
    }

    private static (double Low, double High) NormalizeStylusCalibrationRange(double low, double high)
    {
        var normalizedLow = ClampUnitInterval(low);
        var normalizedHigh = ClampUnitInterval(high);
        if (normalizedHigh - normalizedLow >= StylusRuntimeDefaults.CalibratedRangeSeedMinWidth)
        {
            return (normalizedLow, normalizedHigh);
        }

        return (StylusRuntimeDefaults.CalibratedLowDefault, StylusRuntimeDefaults.CalibratedHighDefault);
    }

    private static bool HasGeometry(AppSettings settings)
    {
        return settings.RollCallWindowWidth > 0
               && settings.RollCallWindowHeight > 0
               && settings.RollCallWindowX != AppSettings.UnsetPosition
               && settings.RollCallWindowY != AppSettings.UnsetPosition;
    }

    private static bool TryParseGeometry(string value, out int width, out int height, out int x, out int y)
    {
        width = 0;
        height = 0;
        x = 0;
        y = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }
        var match = GeometryRegex.Match(value.Trim());
        if (!match.Success)
        {
            return false;
        }
        if (!int.TryParse(match.Groups["w"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out width))
        {
            return false;
        }
        if (!int.TryParse(match.Groups["h"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out height))
        {
            return false;
        }
        if (!int.TryParse(match.Groups["x"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out x))
        {
            return false;
        }
        if (!int.TryParse(match.Groups["y"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out y))
        {
            return false;
        }
        return width > 0 && height > 0;
    }

    private static string FormatGeometry(int width, int height, int x, int y)
    {
        var safeWidth = Math.Max(1, width);
        var safeHeight = Math.Max(1, height);
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{safeWidth}x{safeHeight}{x:+#;-#;0}{y:+#;-#;0}");
    }

    private static List<string> ParseList(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return new List<string>();
        }
        return raw.Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }

    private static string JoinList(IReadOnlyList<string> items)
    {
        if (items == null || items.Count == 0)
        {
            return string.Empty;
        }
        return string.Join("|", items.Where(item => !string.IsNullOrWhiteSpace(item)));
    }
}
