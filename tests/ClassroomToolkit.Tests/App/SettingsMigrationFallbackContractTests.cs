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
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "App.xaml.cs");
    }
}
