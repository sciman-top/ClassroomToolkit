using System.IO;
using System.Text.Json;
using FluentAssertions;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Startup;

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
        app.Should().Contain("AutoUpdateBootstrapper.Schedule(settings);");

        var bootstrapper = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "Startup",
            "AutoUpdateBootstrapper.cs"));
        bootstrapper.Should().Contain("settings.UpdateAutoCheckEnabled");
    }

    [Fact]
    public void PackagedBuild_ShouldExcludeLocalClassroomDataAndUsePersistentDataRoot()
    {
        var project = File.ReadAllText(TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "ClassroomToolkit.App.csproj"));
        var locator = File.ReadAllText(TestPathHelper.ResolveRepoPath("src", "ClassroomToolkit.App", "Helpers", "StudentResourceLocator.cs"));

        project.Should().Contain("'$(IncludeLocalClassroomData)' == 'true'");
        project.Should().Contain("PackageReference Include=\"Velopack\" Version=\"1.2.0\"");
        locator.Should().Contain("Environment.SpecialFolder.LocalApplicationData");
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

    [Fact]
    public void PortableRuntime_ShouldUseAnExplicitMarkerAndPortableDataRoot()
    {
        var context = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src", "ClassroomToolkit.App", "Helpers", "PortableRuntimeContext.cs"));
        var updater = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src", "ClassroomToolkit.App", "Startup", "PortableUpdateBootstrapper.cs"));

        context.Should().Contain("portable.mode");
        context.Should().Contain("Path.Combine(root, DataFolderName)");
        updater.Should().NotContain("api.github.com");
        updater.Should().NotContain("notify-and-open-download-page");
        updater.Should().Contain("ReleasesPageUrl");
        updater.Should().Contain("GetStringAsync");
        updater.Should().Contain("TimeSpan.FromSeconds(5)");

        var capture = File.ReadAllText(TestPathHelper.ResolveRepoPath(
            "src", "ClassroomToolkit.App", "Paint", "RegionScreenCaptureWorkflow.cs"));
        capture.Should().Contain("PortableRuntimeContext.DataDirectory");
    }

    [Theory]
    [InlineData("1.0.8", "v1.0.9", true)]
    [InlineData("1.0.9", "v1.0.9", false)]
    [InlineData("1.0.9", "draft-1.0.10", false)]
    public void PortableReleaseVersion_ShouldCompareReleaseTagsSafely(string current, string candidate, bool expected)
    {
        PortableReleaseVersion.IsNewer(current, candidate).Should().Be(expected);
    }

    [Fact]
    public void Schedule_ShouldThrowArgumentNullException_WhenSettingsIsNull()
    {
        var act = () => AutoUpdateBootstrapper.Schedule(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Schedule_WhenAutoCheckDisabled_ShouldNotTouchCheckStateFile()
    {
        var statePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassroomToolkit",
            "last-update-check-utc.txt");
        var existedBefore = File.Exists(statePath);
        var timestampBefore = existedBefore ? File.GetLastWriteTimeUtc(statePath) : DateTime.MinValue;

        AutoUpdateBootstrapper.Schedule(new AppSettings { UpdateAutoCheckEnabled = false });

        File.Exists(statePath).Should().Be(existedBefore);
        if (existedBefore)
        {
            File.GetLastWriteTimeUtc(statePath).Should().Be(timestampBefore);
        }
    }

    [Fact]
    public void Schedule_WhenNotInstalledByVelopack_ShouldNotTouchCheckStateFile()
    {
        // 测试 bin 因项目引用带入了 enabled=true 的 update-feed.json；
        // 宿主并非 Velopack 安装，CheckAndDownloadAsync 必须在 MarkCheckStarted 与网络之前短路。
        File.Exists(Path.Combine(AppContext.BaseDirectory, "update-feed.json")).Should().BeTrue();

        var statePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ClassroomToolkit",
            "last-update-check-utc.txt");
        var existedBefore = File.Exists(statePath);
        var timestampBefore = existedBefore ? File.GetLastWriteTimeUtc(statePath) : DateTime.MinValue;

        AutoUpdateBootstrapper.Schedule(new AppSettings { UpdateAutoCheckEnabled = true });

        File.Exists(statePath).Should().Be(existedBefore);
        if (existedBefore)
        {
            File.GetLastWriteTimeUtc(statePath).Should().Be(timestampBefore);
        }
    }
}
