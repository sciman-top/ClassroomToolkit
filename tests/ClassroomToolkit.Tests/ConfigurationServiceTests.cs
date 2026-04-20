using ClassroomToolkit.App.Settings;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public void Constructor_ShouldUseDefaultSettingsJson_WhenAppSettingsDoesNotExist()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            var service = new ConfigurationService(baseDirectory);

            service.BaseDirectory.Should().Be(Path.GetFullPath(baseDirectory));
            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.Combine(baseDirectory, "settings.json"));
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
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Ini);
            service.SettingsDocumentPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "config\\custom.ini")));
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
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Ini);
            service.SettingsDocumentPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "nested\\settings.ini")));
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
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.Combine(baseDirectory, "settings.json"));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldFallbackToDefault_WhenAppSettingsIsUnreadable()
    {
        var baseDirectory = CreateTempDirectory();
        var appSettingsPath = Path.Combine(baseDirectory, "appsettings.json");
        try
        {
            File.WriteAllText(appSettingsPath, "{ \"SettingsDocumentFormat\": \"ini\" }");
            using var lockStream = new FileStream(
                appSettingsPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.Combine(baseDirectory, "settings.json"));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldDefaultToIniDocument_WhenAppSettingsExistsWithoutSettingsKeys()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(baseDirectory, "appsettings.json"),
                """
                {
                  "Logging": {
                    "LogLevel": {
                      "Default": "Information"
                    }
                  }
                }
                """);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Ini);
            service.SettingsDocumentPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
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

            service.BaseDirectory.Should().Be(solutionRoot);
            service.SettingsIniPath.Should().Be(Path.Combine(solutionRoot, "settings.ini"));
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.Combine(solutionRoot, "settings.json"));
        }
        finally
        {
            Directory.Delete(solutionRoot, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldUseExecutableDirectory_WhenSolutionFileNotFound()
    {
        var executableDirectory = Path.Combine(@"Z:\", $"ctool_config_no_sln_{Guid.NewGuid():N}");
        var service = new ConfigurationService(executableDirectory);

        service.BaseDirectory.Should().Be(Path.GetFullPath(executableDirectory));
        service.SettingsIniPath.Should().Be(Path.Combine(executableDirectory, "settings.ini"));
        service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
        service.SettingsDocumentPath.Should().Be(Path.Combine(executableDirectory, "settings.json"));
    }

    [Fact]
    public void Constructor_ShouldUseJsonDocumentStore_WhenConfigured()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(baseDirectory, "appsettings.json"),
                """
                {
                  "SettingsDocumentFormat": "json",
                  "SettingsDocumentPath": "config\\settings.json"
                }
                """);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "config\\settings.json")));
            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldInferJsonDocumentStore_WhenPathIsJson()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(baseDirectory, "appsettings.json"),
                """
                {
                  "SettingsDocumentPath": "settings.custom.json"
                }
                """);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "settings.custom.json")));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldFallbackToDefaults_WhenConfiguredSettingsPathIsInvalid()
    {
        var baseDirectory = CreateTempDirectory();
        try
        {
            File.WriteAllText(
                Path.Combine(baseDirectory, "appsettings.json"),
                """
                {
                  "SettingsIniPath": "config\u0000bad.ini",
                  "SettingsDocumentPath": "settings.custom.json"
                }
                """);

            var service = new ConfigurationService(baseDirectory);

            service.SettingsIniPath.Should().Be(Path.Combine(baseDirectory, "settings.ini"));
            service.SettingsDocumentFormat.Should().Be(SettingsDocumentFormat.Json);
            service.SettingsDocumentPath.Should().Be(Path.GetFullPath(Path.Combine(baseDirectory, "settings.custom.json")));
        }
        finally
        {
            Directory.Delete(baseDirectory, recursive: true);
        }
    }

    [Fact]
    public void Constructor_ShouldFallbackToRuntimeBaseDirectory_WhenInputBaseDirectoryIsInvalid()
    {
        var invalidBaseDirectory = "\u0000";
        var baseline = new ConfigurationService();

        var service = new ConfigurationService(invalidBaseDirectory);

        service.BaseDirectory.Should().Be(baseline.BaseDirectory);
        service.SettingsIniPath.Should().Be(baseline.SettingsIniPath);
        service.SettingsDocumentPath.Should().Be(baseline.SettingsDocumentPath);
    }

    private static string CreateTempDirectory()
    {
        var path = TestPathHelper.CreateDirectory("ctool_config");
        File.WriteAllText(Path.Combine(path, "ClassroomToolkit.sln"), "mock-sln");
        return path;
    }
}
