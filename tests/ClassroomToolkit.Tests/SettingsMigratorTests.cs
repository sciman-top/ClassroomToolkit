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
    }
}
