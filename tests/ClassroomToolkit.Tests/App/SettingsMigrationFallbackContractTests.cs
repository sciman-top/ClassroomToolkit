using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class SettingsMigrationFallbackContractTests
{
    [Fact]
    public void AppBootstrap_ShouldFallbackToIniStore_WhenJsonBootstrapMigrationFails()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var fallbackToIni = TryBootstrapSettingsDocumentMigration(configuration);");
        source.Should().Contain("if (fallbackToIni)");
        source.Should().Contain("new SettingsDocumentStoreAdapter(configuration.SettingsIniPath)");
        source.Should().Contain("var fallbackToIni = decision.ShouldMigrate && !migrated;");
    }

    private static string GetSourcePath()
    {
        return Path.Combine(
            FindRepositoryRoot(new DirectoryInfo(AppContext.BaseDirectory))!.FullName,
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }

    private static DirectoryInfo? FindRepositoryRoot(DirectoryInfo? start)
    {
        var current = start;
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "ClassroomToolkit.sln")))
            {
                return current;
            }

            current = current.Parent;
        }

        return null;
    }
}
