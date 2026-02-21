using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public void Constructor_ShouldUseDefaultSettingsIni_WhenAppSettingsDoesNotExist()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldUseDirectSettingsIniPath_WhenConfigured()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(baseDirectory, "appsettings.json"),
                """
                {
                  "SettingsIniPath": "config\\custom.ini"
                }
                """);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "config\\custom.ini")));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldUseNestedPath_WhenConfiguredInPathsNode()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(baseDirectory, "appsettings.json"),
                """
                {
                  "Paths": {
                    "SettingsIni": "nested\\settings.ini"
                  }
                }
                """);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "nested\\settings.ini")));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldFallbackToDefault_WhenAppSettingsMalformed()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(Path.Combine(baseDirectory, "appsettings.json"), "{not-json");

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldUseSolutionRootSettingsIni_WhenRunningFromBuildOutput()
    {
        var solutionRoot = CreateTempDirectory();
        var outputDirectory = Path.Combine(solutionRoot, "src", "ClassroomToolkit.App", "bin", "Release", "net8.0-windows");
        Directory.CreateDirectory(outputDirectory);
        File.WriteAllText(Path.Combine(solutionRoot, "ClassroomToolkit.sln"), string.Empty);

        try
        {
            var service = new ConfigurationService(outputDirectory);

            service.SettingsIniPath.Should().Be(Path.Combine(solutionRoot, "settings.ini"));
        }
        finally
        {
            Directory.Delete(solutionRoot, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldUseEnvironmentOverride_WhenSet()
    {
        var baseDirectory = CreateTempDirectory();
        var previous = Environment.GetEnvironmentVariable("CTK_SETTINGS_INI");
        var expected = Path.Combine(baseDirectory, "custom", "settings.ini");
        try
        {
            Environment.SetEnvironmentVariable("CTK_SETTINGS_INI", expected);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(expected);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CTK_SETTINGS_INI", previous);
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ctool_config_{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
