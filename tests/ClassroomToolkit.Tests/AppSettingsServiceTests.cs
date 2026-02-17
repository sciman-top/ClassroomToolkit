using ClassroomToolkit.App.Settings;
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
}
