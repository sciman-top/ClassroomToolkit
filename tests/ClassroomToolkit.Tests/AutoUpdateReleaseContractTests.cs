using System.IO;
using System.Text.Json;
using AwesomeAssertions;
using ClassroomToolkit.App.Settings;
using ClassroomToolkit.App.Startup;

namespace ClassroomToolkit.Tests;

public sealed class AutoUpdateReleaseContractTests
{
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
