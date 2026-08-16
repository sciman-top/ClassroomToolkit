using ClassroomToolkit.Infra.Migration;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsMigratorTests
{
    [Fact]
    public void Migrate_ShouldThrow_WhenDataIsNull()
    {
        Action act = () => SettingsMigrator.Migrate(null!);

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

        SettingsMigrator.Migrate(data);

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

        SettingsMigrator.Migrate(data);

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

        SettingsMigrator.Migrate(data);

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

        SettingsMigrator.Migrate(data);

        data["Paint"]["office_input_mode"].Should().Be("auto");
        data["Paint"]["wps_input_mode"].Should().Be("raw");
    }

    [Fact]
    public void Migrate_ShouldNotCreateBackupDuringInMemoryTransformation()
    {
        var directory = TestPathHelper.CreateDirectory("ctool_settings_migrator_backup");
        var settingsPath = Path.Combine(directory, "settings.ini");
        File.WriteAllText(settingsPath, "dummy");

        try
        {
            var data = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["Paint"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["wps_input_mode"] = "manual",
                    ["wps_raw_input"] = "True"
                }
            };

            SettingsMigrator.Migrate(data);

            var backups = Directory.GetFiles(directory, "settings.bak-*.*", SearchOption.TopDirectoryOnly);
            backups.Should().BeEmpty();
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
