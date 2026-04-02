using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallWindowSettingsReloadContractTests
{
    [Fact]
    public void Constructor_ShouldReloadSettingsSnapshot_BeforeApplyWindowBounds()
    {
        var source = File.ReadAllText(GetSourcePath());

        var refreshIndex = source.IndexOf("RefreshRollCallSettingsSnapshot();", StringComparison.Ordinal);
        var applyBoundsIndex = source.IndexOf("ApplyWindowBounds(settings);", StringComparison.Ordinal);

        refreshIndex.Should().BeGreaterThan(0);
        applyBoundsIndex.Should().BeGreaterThan(0);
        refreshIndex.Should().BeLessThan(applyBoundsIndex);
    }

    [Fact]
    public void RefreshRollCallSettingsSnapshot_ShouldLoadFromStore_AndSyncRollCallFields()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("_settingsService.Load()");
        source.Should().Contain("_settings.RollCallShowId = latest.RollCallShowId;");
        source.Should().Contain("_settings.RollCallShowPhoto = latest.RollCallShowPhoto;");
        source.Should().Contain("_settings.RollCallTimerMode = latest.RollCallTimerMode;");
        source.Should().Contain("_settings.RemotePresenterKey = latest.RemotePresenterKey;");
        source.Should().Contain("_settings.RollCallCurrentGroup = latest.RollCallCurrentGroup;");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.xaml.cs");
    }
}
