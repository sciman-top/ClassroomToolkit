using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Application.Abstractions;
using ClassroomToolkit.Infra.Settings;
using FluentAssertions;
using System.Text.Json;

namespace ClassroomToolkit.Tests;

public sealed class AppSettingsServiceTests
{
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

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.WpsDebounceMs.Should().Be(120);
            reloaded.PresentationLockStrategyWhenDegraded.Should().BeFalse();
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
            settings.WpsInputMode.Should().Be(WpsInputModeDefaults.Message);
            settings.OfficeInputMode.Should().Be(WpsInputModeDefaults.Auto);
            settings.StylusAdaptivePressureProfile.Should().Be(0);
            settings.StylusAdaptiveSampleRateTier.Should().Be(0);
            settings.StylusAdaptivePredictionHorizonMs.Should().Be(18);
            settings.StylusPressureCalibratedLow.Should().Be(0.0);
            settings.StylusPressureCalibratedHigh.Should().Be(1.0);
            settings.WpsDebounceMs.Should().Be(0);
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
