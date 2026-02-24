using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Ink;
using ClassroomToolkit.App.Paint;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AppSettingsServiceTests
{
    [Fact]
    public void Constructor_ShouldThrow_WhenConfigurationServiceIsNull()
    {
        Action act = () => new AppSettingsService((IConfigurationService)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_ShouldThrow_WhenSettingsPathIsNull()
    {
        Action act = () => new AppSettingsService((string)null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("yes", true)]
    [InlineData("on", true)]
    [InlineData("no", false)]
    [InlineData("off", false)]
    public void Load_ShouldParseBooleanAliases(string raw, bool expected)
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_bool_{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, $"[Paint]\ncontrol_ms_ppt={raw}\n");
            var service = new AppSettingsService(path);

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
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Paint]\ncontrol_ms_ppt=INVALID_BOOL\n");
            var service = new AppSettingsService(path);

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
    public void SaveAndLoad_ShouldPersistPhotoUnifiedTransformState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            var service = new AppSettingsService(path);
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
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            var service = new AppSettingsService(path);
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
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            var service = new AppSettingsService(path);
            var initial = service.Load();
            initial.InkExportScope = InkExportScope.SessionChangesOnly;
            initial.InkExportMaxParallelFiles = 3;
            initial.PhotoNeighborPrefetchRadiusMax = 2;
            initial.PhotoPostInputRefreshDelayMs = 120;
            initial.PhotoWheelZoomBase = 1.001;
            initial.PhotoGestureZoomSensitivity = 1.2;
            initial.PhotoInputTelemetryEnabled = true;

            service.Save(initial);
            var reloaded = service.Load();

            reloaded.InkExportScope.Should().Be(InkExportScope.SessionChangesOnly);
            reloaded.InkExportMaxParallelFiles.Should().Be(3);
            reloaded.PhotoNeighborPrefetchRadiusMax.Should().Be(2);
            reloaded.PhotoPostInputRefreshDelayMs.Should().Be(120);
            reloaded.PhotoWheelZoomBase.Should().BeApproximately(1.001, 0.0001);
            reloaded.PhotoGestureZoomSensitivity.Should().BeApproximately(1.2, 0.0001);
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
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            var service = new AppSettingsService(path);
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
    public void SaveAndLoad_ShouldPersistClassroomWritingMode()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            var service = new AppSettingsService(path);
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
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            File.WriteAllText(path, "[Paint]\nclassroom_writing_mode=INVALID_MODE\n");
            var service = new AppSettingsService(path);

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
        var path = Path.Combine(Path.GetTempPath(), $"ctool_app_settings_{Guid.NewGuid():N}.ini");
        try
        {
            var service = new AppSettingsService(path);
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
}
