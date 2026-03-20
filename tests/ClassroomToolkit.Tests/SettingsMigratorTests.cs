using ClassroomToolkit.Infra.Migration;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsMigratorTests
{
    [Fact]
    public void Migrate_ShouldThrow_WhenDataIsNull()
    {
        Action act = () => SettingsMigrator.Migrate(null!, null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Migrate_ShouldNormalizeManualModeToRaw()
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["wps_input_mode"] = "manual",
                ["wps_raw_input"] = "True"
            }
        };

        SettingsMigrator.Migrate(data, null);

        data["Paint"]["wps_input_mode"].Should().Be("raw");
        data["Paint"]["office_input_mode"].Should().Be("raw");
    }

    [Fact]
    public void Migrate_ShouldNormalizeManualModeToMessage()
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["wps_input_mode"] = "manual",
                ["wps_raw_input"] = "False"
            }
        };

        SettingsMigrator.Migrate(data, null);

        data["Paint"]["wps_input_mode"].Should().Be("message");
        data["Paint"]["office_input_mode"].Should().Be("auto");
    }

    [Fact]
    public void Migrate_ShouldFallbackInvalidModeToAuto()
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["wps_input_mode"] = "weird"
            }
        };

        SettingsMigrator.Migrate(data, null);

        data["Paint"]["wps_input_mode"].Should().Be("auto");
        data["Paint"]["office_input_mode"].Should().Be("auto");
    }

    [Fact]
    public void Migrate_ShouldNormalizeOfficeMode_WhenProvided()
    {
        var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["office_input_mode"] = "weird",
                ["wps_input_mode"] = "raw"
            }
        };

        SettingsMigrator.Migrate(data, null);

        data["Paint"]["office_input_mode"].Should().Be("auto");
        data["Paint"]["wps_input_mode"].Should().Be("raw");
    }

    [Fact]
    public void Migrate_ShouldCreateUniqueBackups_OnRepeatedRuns()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_settings_migrator_backup");
        var settingsPath = Path.Combine(directory, "settings.ini");
        File.WriteAllText(settingsPath, "dummy");

        try
        {
            const int runs = 5;
            for (var index = 0; index < runs; index++)
            {
                var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["wps_input_mode"] = "manual",
                        ["wps_raw_input"] = "True"
                    }
                };

                SettingsMigrator.Migrate(data, settingsPath);
            }

            var backups = Directory.GetFiles(directory, "settings.bak-*.*", SearchOption.TopDirectoryOnly);
            backups.Should().HaveCount(runs);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
