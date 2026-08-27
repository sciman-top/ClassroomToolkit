using System.IO;
using System.Text.Json;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class AutoUpdateReleaseContractTests
{
    [Fact]
    public void Application_ShouldInitializeVelopackBeforeWpfStartupAndScheduleUpdateDownload()
    {
        var program = File.ReadAllText(TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Program.cs"));
        var app = File.ReadAllText(TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "App.xaml.cs"));

        program.Should().Contain("VelopackApp.Build().Run();");
        program.Should().Contain("application.InitializeComponent();");
        app.Should().Contain("AutoUpdateBootstrapper.Schedule();");
    }

    [Fact]
    public void PackagedBuild_ShouldExcludeLocalClassroomDataAndUsePersistentDataRoot()
    {
        var project = File.ReadAllText(TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "ClassroomToolkit.App.csproj"));
        var locator = File.ReadAllText(TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Helpers", "StudentResourceLocator.cs"));

        project.Should().Contain("'$(IncludeLocalClassroomData)' == 'true'");
        project.Should().Contain("PackageReference Include=\"Velopack\" Version=\"1.2.0\"");
        locator.Should().Contain("Environment.SpecialFolder.LocalApplicationData");
        locator.Should().Contain("TryMigrateLegacyPackageData");
        locator.Should().Contain(".migration-pending");
    }

    [Fact]
    public void UpdateFeed_ShouldUseAValidHttpsRepositoryAndBoundItsPollInterval()
    {
        using var feed = JsonDocument.Parse(File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "update-feed.json")));

        feed.RootElement.GetProperty("enabled").GetBoolean().Should().BeTrue();
        feed.RootElement.GetProperty("repositoryUrl").GetString().Should().Be("https://github.com/sciman-top/ClassroomToolkit");
        feed.RootElement.GetProperty("checkIntervalHours").GetInt32().Should().BeInRange(1, 168);

        var bootstrapper = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Startup",
            "AutoUpdateBootstrapper.cs"));
        bootstrapper.Should().Contain("PropertyNameCaseInsensitive = true");
        bootstrapper.Should().Contain("new GithubSource(configuration.RepositoryUrl");
    }
}
