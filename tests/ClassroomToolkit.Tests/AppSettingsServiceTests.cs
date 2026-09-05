using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Photos;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Infra.Settings;
using FluentAssertions;
using System.Globalization;
using System.Text.Json;

namespace ClassroomToolkit.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public void Load_ShouldReadThemeFromIniUiSection()
    {
        var path = CreateTempIniPath("ctool_app_settings_theme");
        try
        {
            File.WriteAllText(path, "[UI]\ntheme=Blackboard\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.UiTheme.Should().Be("Blackboard");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistThemeForIniStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_theme");
        try
        {
            var service = CreateService(path);
            var settings = service.Load();
            settings.UiTheme = "Light";

            service.Save(settings);

            service.Load().UiTheme.Should().Be("Light");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldFallbackToDefaultTheme_WhenIniThemeIsUnknown()
    {
        var path = CreateTempIniPath("ctool_app_settings_theme");
        try
        {
            File.WriteAllText(path, "[UI]\ntheme=999\n");
            var service = CreateService(path);

            service.Load().UiTheme.Should().Be("MidnightTeal");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistThemeForJsonStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_theme_json");
        try
        {
            File.WriteAllText(path, "{\"UI\":{\"theme\":\"Blackboard\"}}");
            var service = CreateJsonService(path);

            var settings = service.Load();
            settings.UiTheme.Should().Be("Blackboard");
            settings.UiTheme = "Light";

            service.Save(settings);

            service.Load().UiTheme.Should().Be("Light");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldKeepLegacyDefaults_WhenUiSectionIsMissing()
    {
        var path = CreateTempIniPath("ctool_app_settings_legacy");
        try
        {
            File.WriteAllText(path, "[Paint]\nbrush_base_size=12\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.UiTheme.Should().Be("MidnightTeal");
            settings.BrushSize.Should().Be(12);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistUpdateAutoCheckEnabled()
    {
        var path = CreateTempIniPath("ctool_app_settings_update");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.UpdateAutoCheckEnabled.Should().BeTrue();
            initial.UpdateAutoCheckEnabled = false;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.UpdateAutoCheckEnabled.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Load_ShouldRespectPersistedUpdateAutoCheckEnabled(bool persistedValue)
    {
        var path = CreateTempIniPath("ctool_app_settings_update_read");
        try
        {
            File.WriteAllText(path, $"[Update]\nauto_check_enabled={(persistedValue ? "True" : "False")}\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.UpdateAutoCheckEnabled.Should().Be(persistedValue);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldFallbackToUpdateAutoCheckDefault_WhenUpdateSectionIsMissingOrInvalid()
    {
        var path = CreateTempIniPath("ctool_app_settings_update_invalid");
        try
        {
            File.WriteAllText(path, "[Update]\nauto_check_enabled=NOT_A_BOOL\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.UpdateAutoCheckEnabled.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenStoreIsNull()
    {
        Action act = () => new AppSettingsService((SettingsDocumentStoreAdapter)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public void Load_ShouldParseBooleanAliases(string raw, bool expected)
    {
        var path = CreateTempIniPath("ctool_app_settings_bool");
        try
        {
            File.WriteAllText(path, $"[Paint]\ncontrol_ms_ppt={raw}\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.ControlMsPpt.Should().Be(expected);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldFallbackForInvalidBooleanText()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            File.WriteAllText(path, "[Paint]\ncontrol_ms_ppt=INVALID_BOOL\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.ControlMsPpt.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldReadLegacyRollCallSectionName()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            File.WriteAllText(
                path,
                """
                [RollCall]
                show_photo=True
                photo_duration_seconds=7
                current_group=第2组
                """);
            var service = CreateService(path);

            var settings = service.Load();

            settings.RollCallShowPhoto.Should().BeTrue();
            settings.RollCallPhotoDurationSeconds.Should().Be(7);
            settings.RollCallCurrentGroup.Should().Be("第2组");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistPhotoUnifiedTransformState()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.PhotoUnifiedTransformEnabled = true;
            initial.PhotoUnifiedScaleX = 1.25;
            initial.PhotoUnifiedScaleY = 1.1;
            initial.PhotoUnifiedTranslateX = 42.5;
            initial.PhotoUnifiedTranslateY = -18.0;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.PhotoUnifiedTransformEnabled.Should().BeTrue();
            reloaded.PhotoUnifiedScaleX.Should().BeApproximately(1.25, 0.0001);
            reloaded.PhotoUnifiedScaleY.Should().BeApproximately(1.1, 0.0001);
            reloaded.PhotoUnifiedTranslateX.Should().BeApproximately(42.5, 0.0001);
            reloaded.PhotoUnifiedTranslateY.Should().BeApproximately(-18.0, 0.0001);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistQuickBrushSizePresets()
    {
        var path = CreateTempIniPath("ctool_app_settings_brush_sizes");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.BrushSize = 16;
            initial.QuickBrushSize1 = 5;
            initial.QuickBrushSize2 = 13;
            initial.QuickBrushSize3 = 31;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.BrushSize.Should().Be(16);
            reloaded.QuickBrushSize1.Should().Be(5);
            reloaded.QuickBrushSize2.Should().Be(13);
            reloaded.QuickBrushSize3.Should().Be(31);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldClampQuickBrushSizePresets()
    {
        var path = CreateTempIniPath("ctool_app_settings_brush_size_clamp");
        try
        {
            File.WriteAllText(
                path,
                """
                [Paint]
                brush_base_size=0
                quick_brush_size_1=-2
                quick_brush_size_2=12
                quick_brush_size_3=200
                """);
            var service = CreateService(path);

            var settings = service.Load();

            settings.BrushSize.Should().Be(1);
            settings.QuickBrushSize1.Should().Be(1);
            settings.QuickBrushSize2.Should().Be(12);
            settings.QuickBrushSize3.Should().Be(50);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldFallbackQuickBrushSizePresets_WhenValuesAreNonFinite()
    {
        var path = CreateTempIniPath("ctool_app_settings_brush_size_non_finite");
        try
        {
            File.WriteAllText(
                path,
                """
                [Paint]
                brush_base_size=NaN
                quick_brush_size_1=NaN
                quick_brush_size_2=Infinity
                quick_brush_size_3=-Infinity
                """);
            var service = CreateService(path);

            var settings = service.Load();

            settings.BrushSize.Should().Be(12);
            settings.QuickBrushSize1.Should().Be(6);
            settings.QuickBrushSize2.Should().Be(12);
            settings.QuickBrushSize3.Should().Be(24);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldNormalizeNonFinitePhotoAndEraserSettings()
    {
        var path = CreateTempIniPath("ctool_app_settings_non_finite_photo");
        try
        {
            File.WriteAllText(
                path,
                """
                [Paint]
                eraser_size=Infinity
                toolbar_scale=NaN
                photo_wheel_zoom_base=NaN
                photo_gesture_zoom_sensitivity=Infinity
                photo_manager_window_width=-1
                photo_manager_window_height=-2
                photo_manager_left_panel_ratio=NaN
                photo_manager_left_panel_width=-3
                photo_manager_thumbnail_size=-Infinity
                photo_unified_scale_x=Infinity
                photo_unified_scale_y=NaN
                photo_unified_translate_x=Infinity
                photo_unified_translate_y=-Infinity
                """);

            var settings = CreateService(path).Load();

            settings.EraserSize.Should().Be(PaintSettingsOptionDefaults.EraserSizeDefault);
            settings.PaintToolbarScale.Should().Be(ToolbarScaleDefaults.Default);
            settings.PhotoWheelZoomBase.Should().Be(PhotoZoomInputDefaults.WheelZoomBaseDefault);
            settings.PhotoGestureZoomSensitivity.Should().Be(PhotoZoomInputDefaults.GestureSensitivityDefault);
            settings.PhotoManagerWindowWidth.Should().Be(0);
            settings.PhotoManagerWindowHeight.Should().Be(0);
            settings.PhotoManagerLeftPanelRatio.Should().Be(ImageManagerWindow.DefaultLeftRatio);
            settings.PhotoManagerLeftPanelWidth.Should().Be(0);
            settings.PhotoManagerThumbnailSize.Should().Be(ImageManagerWindow.DefaultThumbnailSize);
            settings.PhotoUnifiedScaleX.Should().Be(PhotoTransformViewportDefaults.DefaultScale);
            settings.PhotoUnifiedScaleY.Should().Be(PhotoTransformViewportDefaults.DefaultScale);
            settings.PhotoUnifiedTranslateX.Should().Be(PhotoUnifiedTransformDefaults.DefaultTranslateDip);
            settings.PhotoUnifiedTranslateY.Should().Be(PhotoUnifiedTransformDefaults.DefaultTranslateDip);
        }
        finally
        {
            DeleteSettingsArtifacts(path);
        }
    }

    [Fact]
    public void Save_ShouldNotPersistNonFinitePhotoAndEraserSettings_ForJsonStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_non_finite_photo_json");
        try
        {
            var service = CreateJsonService(path);
            var settings = service.Load();
            settings.EraserSize = double.NaN;
            settings.PaintToolbarScale = double.PositiveInfinity;
            settings.PhotoWheelZoomBase = double.NaN;
            settings.PhotoGestureZoomSensitivity = double.NegativeInfinity;
            settings.PhotoManagerWindowWidth = -1;
            settings.PhotoManagerWindowHeight = -2;
            settings.PhotoManagerLeftPanelRatio = double.NaN;
            settings.PhotoManagerLeftPanelWidth = -3;
            settings.PhotoManagerThumbnailSize = double.PositiveInfinity;
            settings.PhotoUnifiedScaleX = double.PositiveInfinity;
            settings.PhotoUnifiedScaleY = double.NaN;
            settings.PhotoUnifiedTranslateX = double.PositiveInfinity;
            settings.PhotoUnifiedTranslateY = double.NegativeInfinity;

            service.Save(settings);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var paint = document.RootElement.GetProperty("Paint");
            var eraser = double.Parse(paint.GetProperty("eraser_size").GetString()!, CultureInfo.InvariantCulture);
            var toolbarScale = double.Parse(paint.GetProperty("toolbar_scale").GetString()!, CultureInfo.InvariantCulture);
            var wheelZoomBase = double.Parse(paint.GetProperty("photo_wheel_zoom_base").GetString()!, CultureInfo.InvariantCulture);
            var gestureSensitivity = double.Parse(paint.GetProperty("photo_gesture_zoom_sensitivity").GetString()!, CultureInfo.InvariantCulture);
            var leftRatio = double.Parse(paint.GetProperty("photo_manager_left_panel_ratio").GetString()!, CultureInfo.InvariantCulture);
            var thumbnailSize = double.Parse(paint.GetProperty("photo_manager_thumbnail_size").GetString()!, CultureInfo.InvariantCulture);
            var scaleX = double.Parse(paint.GetProperty("photo_unified_scale_x").GetString()!, CultureInfo.InvariantCulture);
            var scaleY = double.Parse(paint.GetProperty("photo_unified_scale_y").GetString()!, CultureInfo.InvariantCulture);
            var translateX = double.Parse(paint.GetProperty("photo_unified_translate_x").GetString()!, CultureInfo.InvariantCulture);
            var translateY = double.Parse(paint.GetProperty("photo_unified_translate_y").GetString()!, CultureInfo.InvariantCulture);

            new[]
            {
                eraser,
                toolbarScale,
                wheelZoomBase,
                gestureSensitivity,
                leftRatio,
                thumbnailSize,
                scaleX,
                scaleY,
                translateX,
                translateY
            }.Should().AllSatisfy(value => double.IsFinite(value).Should().BeTrue());
            eraser.Should().Be(PaintSettingsOptionDefaults.EraserSizeDefault);
            toolbarScale.Should().Be(ToolbarScaleDefaults.Default);
            wheelZoomBase.Should().Be(PhotoZoomInputDefaults.WheelZoomBaseDefault);
            gestureSensitivity.Should().Be(PhotoZoomInputDefaults.GestureSensitivityDefault);
            leftRatio.Should().BeApproximately(ImageManagerWindow.DefaultLeftRatio, 0.0001);
            thumbnailSize.Should().Be(ImageManagerWindow.DefaultThumbnailSize);
            scaleX.Should().Be(PhotoTransformViewportDefaults.DefaultScale);
            scaleY.Should().Be(PhotoTransformViewportDefaults.DefaultScale);
            translateX.Should().Be(PhotoUnifiedTransformDefaults.DefaultTranslateDip);
            translateY.Should().Be(PhotoUnifiedTransformDefaults.DefaultTranslateDip);
            paint.GetProperty("photo_manager_window_width").GetString().Should().Be("0");
            paint.GetProperty("photo_manager_window_height").GetString().Should().Be("0");
            paint.GetProperty("photo_manager_left_panel_width").GetString().Should().Be("0");
        }
        finally
        {
            DeleteSettingsArtifacts(path);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistPhotoShowInkOverlayState()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.PhotoShowInkOverlay = false;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.PhotoShowInkOverlay.Should().BeFalse();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LoadAndSave_ShouldMigrateLegacyInitializationFlagsAndDropEphemeralKeys(bool useJsonStore)
    {
        var path = CreateTempIniPath(
            useJsonStore ? "ctool_app_settings_legacy_flags_json" : "ctool_app_settings_legacy_flags");
        try
        {
            File.WriteAllText(
                path,
                useJsonStore
                    ? """
                    {
                      "Launcher": {
                        "ui_defaults_optimized": "True"
                      },
                      "Paint": {
                        "preset_recommendation_initialized": "True",
                        "board_opacity": "17"
                      },
                      "RollCallTimer": {
                        "timer_running": "True",
                        "id_font_size": "48",
                        "name_font_size": "60",
                        "timer_font_size": "56"
                      },
                      "RollCall": {
                        "timer_running": "True",
                        "id_font_size": "48",
                        "name_font_size": "60",
                        "timer_font_size": "56"
                      }
                    }
                    """
                    : """
                    [Launcher]
                    ui_defaults_optimized=True

                    [Paint]
                    preset_recommendation_initialized=True
                    board_opacity=17

                    [RollCallTimer]
                    timer_running=True
                    id_font_size=48
                    name_font_size=60
                    timer_font_size=56

                    [RollCall]
                    timer_running=True
                    id_font_size=48
                    name_font_size=60
                    timer_font_size=56
                    """);

            var service = useJsonStore ? CreateJsonService(path) : CreateService(path);
            var settings = service.Load();

            settings.UiDefaultsVersion.Should().Be(UiDefaultsBootstrapOptimizationPolicy.CurrentVersion);
            settings.PresetRecommendationVersion.Should().Be(PresetSchemeInitializationPolicy.CurrentVersion);

            service.Save(settings);

            if (useJsonStore)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                var launcher = document.RootElement.GetProperty("Launcher");
                var paint = document.RootElement.GetProperty("Paint");
                var roll = document.RootElement.GetProperty("RollCallTimer");
                var legacyRoll = document.RootElement.GetProperty("RollCall");
                launcher.GetProperty("ui_defaults_version").GetString()
                    .Should().Be(UiDefaultsBootstrapOptimizationPolicy.CurrentVersion.ToString());
                paint.GetProperty("preset_recommendation_version").GetString()
                    .Should().Be(PresetSchemeInitializationPolicy.CurrentVersion.ToString());
                launcher.TryGetProperty("ui_defaults_optimized", out _).Should().BeFalse();
                paint.TryGetProperty("preset_recommendation_initialized", out _).Should().BeFalse();
                paint.TryGetProperty("board_opacity", out _).Should().BeFalse();
                roll.TryGetProperty("timer_running", out _).Should().BeFalse();
                roll.TryGetProperty("id_font_size", out _).Should().BeFalse();
                roll.TryGetProperty("name_font_size", out _).Should().BeFalse();
                roll.TryGetProperty("timer_font_size", out _).Should().BeFalse();
                legacyRoll.TryGetProperty("timer_running", out _).Should().BeFalse();
                legacyRoll.TryGetProperty("id_font_size", out _).Should().BeFalse();
                legacyRoll.TryGetProperty("name_font_size", out _).Should().BeFalse();
                legacyRoll.TryGetProperty("timer_font_size", out _).Should().BeFalse();
            }
            else
            {
                var saved = File.ReadAllText(path);
                saved.Should().Contain(
                    $"ui_defaults_version={UiDefaultsBootstrapOptimizationPolicy.CurrentVersion}");
                saved.Should().Contain(
                    $"preset_recommendation_version={PresetSchemeInitializationPolicy.CurrentVersion}");
                saved.Should().NotContain("ui_defaults_optimized");
                saved.Should().NotContain("preset_recommendation_initialized");
                saved.Should().NotContain("board_opacity");
                saved.Should().NotContain("timer_running");
                saved.Should().NotContain("id_font_size");
                saved.Should().NotContain("name_font_size");
                saved.Should().NotContain("timer_font_size");
            }
        }
        finally
        {
            DeleteSettingsArtifacts(path);
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistInkExportScope()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.InkExportScope = InkExportScope.SessionChangesOnly;
            initial.InkExportMaxParallelFiles = 3;
            initial.PhotoNeighborPrefetchRadiusMax = 2;
            initial.PhotoPostInputRefreshDelayMs = 120;
            initial.PhotoWheelZoomBase = 1.001;
            initial.PhotoGestureZoomSensitivity = 1.2;
            initial.PhotoInertiaProfile = PhotoInertiaProfileDefaults.Heavy;
            initial.PhotoInputTelemetryEnabled = true;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.InkExportScope.Should().Be(InkExportScope.SessionChangesOnly);
            reloaded.InkExportMaxParallelFiles.Should().Be(3);
            reloaded.PhotoNeighborPrefetchRadiusMax.Should().Be(2);
            reloaded.PhotoPostInputRefreshDelayMs.Should().Be(120);
            reloaded.PhotoWheelZoomBase.Should().BeApproximately(1.001, 0.0001);
            reloaded.PhotoGestureZoomSensitivity.Should().BeApproximately(1.2, 0.0001);
            reloaded.PhotoInertiaProfile.Should().Be(PhotoInertiaProfileDefaults.Heavy);
            reloaded.PhotoInputTelemetryEnabled.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistPresentationAlignmentOptions()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.WpsDebounceMs = 120;
            initial.PresentationLockStrategyWhenDegraded = false;
            initial.PresentationAutoFallbackFailureThreshold = 3;
            initial.PresentationAutoFallbackProbeIntervalCommands = 12;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.WpsDebounceMs.Should().Be(120);
            reloaded.PresentationLockStrategyWhenDegraded.Should().BeFalse();
            reloaded.PresentationAutoFallbackFailureThreshold.Should().Be(3);
            reloaded.PresentationAutoFallbackProbeIntervalCommands.Should().Be(12);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistPresentationClassifierOverridesJson()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.PresentationClassifierOverridesJson =
                """{"AdditionalWpsClassTokens":["gov-wps-class"],"AdditionalOfficeProcessTokens":["powerpoint_gov"]}""";

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.PresentationClassifierOverridesJson.Should().Be(initial.PresentationClassifierOverridesJson);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistPresentationClassifierAutoLearnEnabled()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.PresentationClassifierAutoLearnEnabled = true;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.PresentationClassifierAutoLearnEnabled.Should().BeTrue();
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistPresentationClassifierLearnHistoryFields()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.PresentationClassifierLastLearnUtc = "2026-03-18T08:30:00.0000000Z";
            initial.PresentationClassifierLastLearnDetail = "type=Office; process=pptgov; classes=GovPptShowClass";
            initial.PresentationClassifierRecentLearnRecordsJson =
                """[{"Utc":"2026-03-18T08:30:00.0000000Z","Detail":"type=Office; process=pptgov"}]""";

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.PresentationClassifierLastLearnUtc.Should().Be(initial.PresentationClassifierLastLearnUtc);
            reloaded.PresentationClassifierLastLearnDetail.Should().Be(initial.PresentationClassifierLastLearnDetail);
            reloaded.PresentationClassifierRecentLearnRecordsJson.Should().Be(initial.PresentationClassifierRecentLearnRecordsJson);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistClassroomWritingMode()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.ClassroomWritingMode = ClassroomWritingMode.Responsive;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.ClassroomWritingMode.Should().Be(ClassroomWritingMode.Responsive);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldFallbackClassroomWritingMode_WhenInvalid()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            File.WriteAllText(path, "[Paint]\nclassroom_writing_mode=INVALID_MODE\n");
            var service = CreateService(path);

            var settings = service.Load();

            settings.ClassroomWritingMode.Should().Be(ClassroomWritingMode.Balanced);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SaveAndLoad_ShouldPersistStylusAdaptiveState()
    {
        var path = CreateTempIniPath("ctool_app_settings");
        try
        {
            var service = CreateService(path);
            var initial = service.Load();
            initial.StylusAdaptivePressureProfile = 1;
            initial.StylusAdaptiveSampleRateTier = 3;
            initial.StylusAdaptivePredictionHorizonMs = 11;
            initial.StylusPressureCalibratedLow = 0.12;
            initial.StylusPressureCalibratedHigh = 0.88;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.StylusAdaptivePressureProfile.Should().Be(1);
            reloaded.StylusAdaptiveSampleRateTier.Should().Be(3);
            reloaded.StylusAdaptivePredictionHorizonMs.Should().Be(11);
            reloaded.StylusPressureCalibratedLow.Should().BeApproximately(0.12, 0.0001);
            reloaded.StylusPressureCalibratedHigh.Should().BeApproximately(0.88, 0.0001);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldNormalizePresetAndAdaptiveSettings_ForJsonStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "Paint": {
                    "preset_scheme": "legacy",
                    "wps_input_mode": "invalid_mode",
                    "wps_debounce_ms": "-12",
                    "presentation_auto_fallback_failure_threshold": "0",
                    "presentation_auto_fallback_probe_interval_commands": "1000",
                    "toolbar_scale": "3.7",
                    "ink_export_max_parallel_files": "-5",
                    "ink_retention_days": "-9",
                    "ink_photo_root_path": "  ",
                    "photo_neighbor_prefetch_radius_max": "999",
                    "photo_post_input_refresh_delay_ms": "1",
                    "photo_wheel_zoom_base": "0.1",
                    "photo_gesture_zoom_sensitivity": "9",
                    "photo_inertia_profile": "legacy_profile",
                    "stylus_adaptive_pressure_profile": "999",
                    "stylus_adaptive_sample_rate_tier": "-2",
                    "stylus_adaptive_prediction_horizon_ms": "999",
                    "stylus_pressure_calibrated_low": "0.92",
                    "stylus_pressure_calibrated_high": "0.925"
                  },
                  "Launcher": {
                    "auto_exit_seconds": "-1"
                  }
                }
                """);
            var service = CreateJsonService(path);

            var settings = service.Load();

            settings.PresetScheme.Should().Be(PresetSchemeDefaults.Custom);
            settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Auto);
            settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Auto);
            settings.StylusAdaptivePressureProfile.Should().Be(0);
            settings.StylusAdaptiveSampleRateTier.Should().Be(0);
            settings.StylusAdaptivePredictionHorizonMs.Should().Be(18);
            settings.StylusPressureCalibratedLow.Should().Be(0.0);
            settings.StylusPressureCalibratedHigh.Should().Be(1.0);
            settings.WpsDebounceMs.Should().Be(0);
            settings.PresentationAutoFallbackFailureThreshold.Should().Be(1);
            settings.PresentationAutoFallbackProbeIntervalCommands.Should().Be(100);
            settings.PaintToolbarScale.Should().Be(ToolbarScaleDefaults.Max);
            settings.InkExportMaxParallelFiles.Should().Be(0);
            settings.InkRetentionDays.Should().Be(0);
            settings.InkPhotoRootPath.Should().Be(AppSettings.ResolveDefaultInkPhotoRootPath());
            settings.PhotoNeighborPrefetchRadiusMax.Should().Be(CrossPageNeighborPrefetchDefaults.RadiusMax);
            settings.PhotoPostInputRefreshDelayMs.Should().Be(CrossPagePostInputRefreshDelayClampPolicy.MinDelayMs);
            settings.PhotoWheelZoomBase.Should().Be(PhotoZoomInputDefaults.WheelZoomBaseMin);
            settings.PhotoGestureZoomSensitivity.Should().Be(PhotoZoomInputDefaults.GestureSensitivityMax);
            settings.PhotoInertiaProfile.Should().Be(PhotoInertiaProfileDefaults.Standard);
            settings.LauncherAutoExitSeconds.Should().Be(0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldMapLegacyManualWpsMode_ForJsonStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "Paint": {
                    "wps_input_mode": "manual",
                    "wps_raw_input": "False"
                  }
                }
                """);
            var service = CreateJsonService(path);

            var settings = service.Load();

            settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Message);
            settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Auto);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Load_ShouldRespectOfficeInputMode_WhenProvided()
    {
        var path = CreateTempIniPath("ctool_app_settings_json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "Paint": {
                    "office_input_mode": "message",
                    "wps_input_mode": "raw"
                  }
                }
                """);
            var service = CreateJsonService(path);

            var settings = service.Load();

            settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Message);
            settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Raw);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_ShouldNormalizeManagedDefaults_ForJsonStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_json");
        try
        {
            var service = CreateJsonService(path);
            var settings = service.Load();
            settings.PresetScheme = "legacy";
            settings.WpsInputMode = "invalid_mode";
            settings.OfficeInputMode = "invalid_mode";
            settings.StylusAdaptivePressureProfile = 99;
            settings.StylusAdaptiveSampleRateTier = -1;
            settings.StylusAdaptivePredictionHorizonMs = 1000;
            settings.StylusPressureCalibratedLow = 0.94;
            settings.StylusPressureCalibratedHigh = 0.945;
            settings.WpsDebounceMs = -99;
            settings.PresentationAutoFallbackFailureThreshold = 999;
            settings.PresentationAutoFallbackProbeIntervalCommands = -1;
            settings.PaintToolbarScale = 0.1;
            settings.InkExportMaxParallelFiles = -6;
            settings.InkRetentionDays = -3;
            settings.InkPhotoRootPath = " ";
            settings.PhotoNeighborPrefetchRadiusMax = -1;
            settings.PhotoPostInputRefreshDelayMs = 9999;
            settings.PhotoWheelZoomBase = 100;
            settings.PhotoGestureZoomSensitivity = 0.01;
            settings.LauncherAutoExitSeconds = -1;

            service.Save(settings);
            var reloaded = service.Load();

            reloaded.PresetScheme.Should().Be(PresetSchemeDefaults.Custom);
            reloaded.WpsInputMode.Should().Be(WpsInputModeDefaults.Auto);
            reloaded.OfficeInputMode.Should().Be(WpsInputModeDefaults.Auto);
            reloaded.StylusAdaptivePressureProfile.Should().Be(0);
            reloaded.StylusAdaptiveSampleRateTier.Should().Be(0);
            reloaded.StylusAdaptivePredictionHorizonMs.Should().Be(18);
            reloaded.StylusPressureCalibratedLow.Should().Be(0.0);
            reloaded.StylusPressureCalibratedHigh.Should().Be(1.0);
            reloaded.WpsDebounceMs.Should().Be(0);
            reloaded.PresentationAutoFallbackFailureThreshold.Should().Be(10);
            reloaded.PresentationAutoFallbackProbeIntervalCommands.Should().Be(1);
            reloaded.PaintToolbarScale.Should().Be(ToolbarScaleDefaults.Min);
            reloaded.InkExportMaxParallelFiles.Should().Be(0);
            reloaded.InkRetentionDays.Should().Be(0);
            reloaded.InkPhotoRootPath.Should().Be(AppSettings.ResolveDefaultInkPhotoRootPath());
            reloaded.PhotoNeighborPrefetchRadiusMax.Should().Be(CrossPageNeighborPrefetchDefaults.RadiusMin);
            reloaded.PhotoPostInputRefreshDelayMs.Should().Be(CrossPagePostInputRefreshDelayClampPolicy.MaxDelayMs);
            reloaded.PhotoWheelZoomBase.Should().Be(PhotoZoomInputDefaults.WheelZoomBaseMax);
            reloaded.PhotoGestureZoomSensitivity.Should().Be(PhotoZoomInputDefaults.GestureSensitivityMin);
            reloaded.LauncherAutoExitSeconds.Should().Be(0);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_ShouldRemoveLegacyWpsRawInputKey_ForJsonStore()
    {
        var path = CreateTempIniPath("ctool_app_settings_json");
        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "Paint": {
                    "wps_input_mode": "manual",
                    "wps_raw_input": "True"
                  }
                }
                """);
            var service = CreateJsonService(path);
            var settings = service.Load();
            settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Raw);
            settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Raw);

            service.Save(settings);

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var paint = document.RootElement.GetProperty("Paint");
            paint.TryGetProperty("wps_raw_input", out _).Should().BeFalse();
            paint.GetProperty("wps_input_mode").GetString().Should().Be(WpsInputModeDefaults.Raw);
            paint.GetProperty("office_input_mode").GetString().Should().Be(WpsInputModeDefaults.Raw);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Save_ShouldThrowArgumentNullException_WhenSettingsIsNull()
    {
        var service = new AppSettingsService(new NullReturningSettingsStore());

        var act = () => service.Save(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Load_ShouldFallbackToDefaults_WhenStoreReturnsNull()
    {
        var service = new AppSettingsService(new NullReturningSettingsStore());
        var defaults = new AppSettings();

        var settings = service.Load();

        settings.RollCallShowId.Should().Be(defaults.RollCallShowId);
        settings.BrushSize.Should().Be(defaults.BrushSize);
        settings.OfficeInputMode.Should().Be(defaults.OfficeInputMode);
        settings.WpsInputMode.Should().Be(defaults.WpsInputMode);
    }

    private static AppSettingsService CreateService(string path)
    {
        return new AppSettingsService(new SettingsDocumentStoreAdapter(path));
    }

    private static AppSettingsService CreateJsonService(string path)
    {
        return new AppSettingsService(new JsonSettingsDocumentStoreAdapter(path));
    }

    private static string CreateTempIniPath(string prefix)
    {
        return TestPathHelper.CreateFilePath(prefix, ".ini");
    }

    private static void DeleteSettingsArtifacts(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        var directory = Path.GetDirectoryName(path);
        var fileName = Path.GetFileNameWithoutExtension(path);
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(fileName))
        {
            return;
        }

        foreach (var backup in Directory.GetFiles(
                     directory,
                     $"{fileName}.bak-v2.0-*{extension}",
                     SearchOption.TopDirectoryOnly))
        {
            File.Delete(backup);
        }
    }

    private sealed class NullReturningSettingsStore : ISettingsDocumentStore
    {
        public Dictionary<string, Dictionary<string, string>> Load()
        {
            return null!;
        }

        public void Save(Dictionary<string, Dictionary<string, string>> data)
        {
        }
    }
}
