using FluentAssertions;

namespace ClassroomToolkit.Tests.App;

public sealed class RollCallWindowSettingsReloadContractTests
{
    [Fact]
    public void Constructor_ShouldUseInjectedSettingsSnapshot_BeforeApplyWindowBounds()
    {
        var source = File.ReadAllText(GetSourcePath());

        var assignSettingsIndex = source.IndexOf("_settings = settings;", StringComparison.Ordinal);
        var applyBoundsIndex = source.IndexOf("ApplyWindowBounds(settings);", StringComparison.Ordinal);

        assignSettingsIndex.Should().BeGreaterThan(0);
        applyBoundsIndex.Should().BeGreaterThan(0);
        assignSettingsIndex.Should().BeLessThan(applyBoundsIndex);
        source.Should().NotContain("RefreshRollCallSettingsSnapshot();");
    }

    [Fact]
    public void Constructor_ShouldNotReloadSettingsFromStore()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().NotContain("_settingsService.Load()");
        source.Should().NotContain("private void RefreshRollCallSettingsSnapshot()");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.xaml.cs");
    }
}
