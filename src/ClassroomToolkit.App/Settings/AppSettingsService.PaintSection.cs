using System.Collections.Generic;
using System.Globalization;

namespace ClassroomToolkit.App.Settings;

public sealed partial class AppSettingsService
{
    private static void ApplyPaintSettings(Dictionary<string, string> paint, AppSettings settings)
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
        settings.OfficeInputMode = ResolveOfficeInputMode(
            paint,
            settings.OfficeInputMode,
            settings.WpsInputMode);
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
        settings.PresentationAutoFallbackFailureThreshold = NormalizePresentationAutoFallbackFailureThreshold(
            GetInt(
                paint,
                "presentation_auto_fallback_failure_threshold",
                settings.PresentationAutoFallbackFailureThreshold));
        settings.PresentationAutoFallbackProbeIntervalCommands = NormalizePresentationAutoFallbackProbeIntervalCommands(
            GetInt(
                paint,
                "presentation_auto_fallback_probe_interval_commands",
                settings.PresentationAutoFallbackProbeIntervalCommands));
        settings.PresentationClassifierAutoLearnEnabled = GetBool(
            paint,
            "presentation_classifier_auto_learn_enabled",
            settings.PresentationClassifierAutoLearnEnabled);
        settings.PresentationClassifierOverridesJson = GetString(
            paint,
            "presentation_classifier_overrides_json",
            settings.PresentationClassifierOverridesJson);
        settings.PresentationClassifierLastLearnUtc = GetString(
            paint,
            "presentation_classifier_last_learn_utc",
            settings.PresentationClassifierLastLearnUtc);
        settings.PresentationClassifierLastLearnDetail = GetString(
            paint,
            "presentation_classifier_last_learn_detail",
            settings.PresentationClassifierLastLearnDetail);
        settings.PresentationClassifierRecentLearnRecordsJson = GetString(
            paint,
            "presentation_classifier_recent_learn_records_json",
            settings.PresentationClassifierRecentLearnRecordsJson);
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
        settings.PhotoInertiaProfile = NormalizePhotoInertiaProfile(
            GetString(
                paint,
                "photo_inertia_profile",
                settings.PhotoInertiaProfile));
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

    private static void SavePaintSettings(
        Dictionary<string, Dictionary<string, string>> data,
        AppSettings settings)
    {
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
        SetBool(paint, "calligraphy_ink_bloom_enabled", settings.CalligraphyInkBloomEnabled);
        SetBool(paint, "calligraphy_seal_enabled", settings.CalligraphySealEnabled);
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
        SetBool(paint, "control_ms_ppt", settings.ControlMsPpt);
        SetBool(paint, "control_wps_ppt", settings.ControlWpsPpt);
        paint["office_input_mode"] = NormalizeInputMode(settings.OfficeInputMode, WpsInputModeDefaults.Auto);
        paint["wps_input_mode"] = NormalizeWpsInputMode(settings.WpsInputMode, WpsInputModeDefaults.Auto);
        SetBool(paint, "wps_wheel_forward", settings.WpsWheelForward);
        SetBool(
            paint,
            "force_presentation_foreground_on_fullscreen",
            settings.ForcePresentationForegroundOnFullscreen);
        paint["wps_debounce_ms"] = NormalizeWpsDebounceMs(settings.WpsDebounceMs).ToString(CultureInfo.InvariantCulture);
        SetBool(
            paint,
            "presentation_lock_strategy_when_degraded",
            settings.PresentationLockStrategyWhenDegraded);
        paint["presentation_auto_fallback_failure_threshold"] =
            NormalizePresentationAutoFallbackFailureThreshold(settings.PresentationAutoFallbackFailureThreshold)
                .ToString(CultureInfo.InvariantCulture);
        paint["presentation_auto_fallback_probe_interval_commands"] =
            NormalizePresentationAutoFallbackProbeIntervalCommands(settings.PresentationAutoFallbackProbeIntervalCommands)
                .ToString(CultureInfo.InvariantCulture);
        SetBool(
            paint,
            "presentation_classifier_auto_learn_enabled",
            settings.PresentationClassifierAutoLearnEnabled);
        paint["presentation_classifier_overrides_json"] =
            settings.PresentationClassifierOverridesJson ?? string.Empty;
        paint["presentation_classifier_last_learn_utc"] =
            settings.PresentationClassifierLastLearnUtc ?? string.Empty;
        paint["presentation_classifier_last_learn_detail"] =
            settings.PresentationClassifierLastLearnDetail ?? string.Empty;
        paint["presentation_classifier_recent_learn_records_json"] =
            settings.PresentationClassifierRecentLearnRecordsJson ?? string.Empty;
        SetBool(
            paint,
            "preset_recommendation_initialized",
            settings.PresetRecommendationInitialized);
        paint["shape_type"] = settings.ShapeType.ToString();
        paint["x"] = settings.PaintToolbarX.ToString(CultureInfo.InvariantCulture);
        paint["y"] = settings.PaintToolbarY.ToString(CultureInfo.InvariantCulture);
        paint["toolbar_scale"] = NormalizePaintToolbarScale(settings.PaintToolbarScale).ToString(CultureInfo.InvariantCulture);
        SetBool(paint, "ink_cache_enabled", settings.InkCacheEnabled);
        SetBool(paint, "ink_save_enabled", settings.InkSaveEnabled);
        paint["ink_export_scope"] = settings.InkExportScope.ToString();
        paint["ink_export_max_parallel_files"] = NormalizeInkExportMaxParallelFiles(settings.InkExportMaxParallelFiles).ToString(CultureInfo.InvariantCulture);
        SetBool(paint, "ink_record_enabled", settings.InkRecordEnabled);
        SetBool(paint, "ink_replay_previous_enabled", settings.InkReplayPreviousEnabled);
        paint["ink_retention_days"] = NormalizeInkRetentionDays(settings.InkRetentionDays).ToString(CultureInfo.InvariantCulture);
        paint["ink_photo_root_path"] = NormalizeInkPhotoRootPath(settings.InkPhotoRootPath);
        paint["photo_favorites"] = JoinList(settings.PhotoFavoriteFolders);
        paint["photo_recent_folders"] = JoinList(settings.PhotoRecentFolders);
        SetBool(paint, "photo_remember_transform", settings.PhotoRememberTransform);
        SetBool(paint, "photo_cross_page_display", settings.PhotoCrossPageDisplay);
        SetBool(paint, "photo_input_telemetry_enabled", settings.PhotoInputTelemetryEnabled);
        paint["photo_neighbor_prefetch_radius_max"] = NormalizePhotoNeighborPrefetchRadiusMax(settings.PhotoNeighborPrefetchRadiusMax).ToString(CultureInfo.InvariantCulture);
        paint["photo_post_input_refresh_delay_ms"] = NormalizePhotoPostInputRefreshDelayMs(settings.PhotoPostInputRefreshDelayMs).ToString(CultureInfo.InvariantCulture);
        paint["photo_wheel_zoom_base"] = NormalizePhotoWheelZoomBase(settings.PhotoWheelZoomBase).ToString("0.####", CultureInfo.InvariantCulture);
        paint["photo_gesture_zoom_sensitivity"] = NormalizePhotoGestureZoomSensitivity(settings.PhotoGestureZoomSensitivity).ToString("0.###", CultureInfo.InvariantCulture);
        paint["photo_inertia_profile"] = NormalizePhotoInertiaProfile(settings.PhotoInertiaProfile);
        SetBool(paint, "photo_show_ink_overlay", settings.PhotoShowInkOverlay);
        paint["photo_manager_window_width"] = settings.PhotoManagerWindowWidth.ToString(CultureInfo.InvariantCulture);
        paint["photo_manager_window_height"] = settings.PhotoManagerWindowHeight.ToString(CultureInfo.InvariantCulture);
        paint["photo_manager_left_panel_ratio"] = settings.PhotoManagerLeftPanelRatio.ToString("0.####", CultureInfo.InvariantCulture);
        paint["photo_manager_left_panel_width"] = settings.PhotoManagerLeftPanelWidth.ToString(CultureInfo.InvariantCulture);
        paint["photo_manager_thumbnail_size"] = settings.PhotoManagerThumbnailSize.ToString("0.##", CultureInfo.InvariantCulture);
        SetBool(paint, "photo_manager_list_mode", settings.PhotoManagerListMode);
        SetBool(paint, "photo_unified_transform_enabled", settings.PhotoUnifiedTransformEnabled);
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
    }
}
