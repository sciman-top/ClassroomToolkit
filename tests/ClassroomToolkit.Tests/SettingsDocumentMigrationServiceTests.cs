using System.Text;
using ClassroomToolkit.Infra.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class SettingsDocumentMigrationServiceTests
{
    [Fact]
    public void MigrateIniToJson_ShouldThrow_WhenIniMissing()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var service = new SettingsDocumentMigrationService();
            var iniPath = Path.Combine(tempDir, "settings.ini");
            var jsonPath = Path.Combine(tempDir, "settings.json");

            Action act = () => service.MigrateIniToJson(iniPath, jsonPath, overwriteJson: false);

            act.Should().Throw<FileNotFoundException>();
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void MigrateIniToJson_ShouldCreateJson_WhenTargetNotExists()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var iniPath = Path.Combine(tempDir, "settings.ini");
            var jsonPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(
                iniPath,
                """
                [Paint]
                brush_base_size=12
                brush_color=#FF000000

                [Launcher]
                x=100
                y=200
                """,
                Encoding.UTF8);

            var service = new SettingsDocumentMigrationService();
            var result = service.MigrateIniToJson(iniPath, jsonPath, overwriteJson: false);

            result.Migrated.Should().BeTrue();
            result.SectionCount.Should().Be(2);
            result.KeyCount.Should().Be(4);
            File.Exists(jsonPath).Should().BeTrue();

            var store = new JsonSettingsDocumentStoreAdapter(jsonPath);
            var loaded = store.Load();
            loaded["Paint"]["brush_base_size"].Should().Be("12");
            loaded["Launcher"]["x"].Should().Be("100");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void MigrateIniToJson_ShouldSkip_WhenTargetExistsAndOverwriteDisabled()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var iniPath = Path.Combine(tempDir, "settings.ini");
            var jsonPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(iniPath, "[Paint]\nbrush_base_size=12\n", Encoding.UTF8);
            File.WriteAllText(jsonPath, "{\"Paint\":{\"brush_base_size\":\"8\"}}", Encoding.UTF8);

            var service = new SettingsDocumentMigrationService();
            var result = service.MigrateIniToJson(iniPath, jsonPath, overwriteJson: false);

            result.Migrated.Should().BeFalse();
            var persisted = File.ReadAllText(jsonPath, Encoding.UTF8);
            persisted.Should().Contain("\"8\"");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void MigrateIniToJson_ShouldBackupAndOverwrite_WhenEnabled()
    {
        var tempDir = CreateTempDirectory();
        try
        {
            var iniPath = Path.Combine(tempDir, "settings.ini");
            var jsonPath = Path.Combine(tempDir, "settings.json");
            File.WriteAllText(iniPath, "[Paint]\nbrush_base_size=12\n", Encoding.UTF8);
            File.WriteAllText(jsonPath, "{\"Paint\":{\"brush_base_size\":\"8\"}}", Encoding.UTF8);

            var service = new SettingsDocumentMigrationService();
            var result = service.MigrateIniToJson(iniPath, jsonPath, overwriteJson: true);

            result.Migrated.Should().BeTrue();
            result.BackupPath.Should().NotBeNullOrWhiteSpace();
            File.Exists(result.BackupPath!).Should().BeTrue();
            var persisted = File.ReadAllText(jsonPath, Encoding.UTF8);
            persisted.Should().Contain("\"12\"");
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        return TestPathHelper.CreateDirectory("ctool_settings_migrate");
    }
}
