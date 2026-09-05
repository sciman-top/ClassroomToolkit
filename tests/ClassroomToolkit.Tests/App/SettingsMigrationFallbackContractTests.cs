using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class SettingsMigrationFallbackContractTests
{
    [Fact]
    public void AppBootstrap_ShouldFallbackToIniStore_WhenJsonBootstrapMigrationFails()
    {
        var appSource = File.ReadAllText(GetSourcePath("App.xaml.cs"));
        var compositionRootSource = File.ReadAllText(GetSourcePath("Startup", "AppCompositionRoot.cs"));

        appSource.Should().Contain("_services = AppCompositionRoot.Build(this, AppDataDirectory);");
        compositionRootSource.Should().Contain("var fallbackToIni = TryBootstrapSettingsDocumentMigration(configuration);");
        compositionRootSource.Should().Contain("if (fallbackToIni)");
        compositionRootSource.Should().Contain("new SettingsDocumentStoreAdapter(configuration.SettingsIniPath)");
        compositionRootSource.Should().Contain("var fallbackToIni = decision.ShouldMigrate && !migrated;");
    }

    private static string GetSourcePath(params string[] relativePath)
    {
        return TestPathHelper.ResolveRepoPath(new[] { "src", "ClassroomToolkit.App" }.Concat(relativePath).ToArray());
    }
}
