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

    [Fact]
    public void PersistSettings_ShouldNotWriteViewModelDefaults_BeforeSnapshotApplied()
    {
        var stateSource = File.ReadAllText(GetStateSourcePath());

        stateSource.Should().Contain("if (!_settingsSnapshotApplied)");
        stateSource.Should().Contain("Skip PersistSettings because settings snapshot has not been applied yet.");
    }

    [Fact]
    public void WarmupThenExit_Path_ShouldBeProtectedBySnapshotGuard_BeforePersistMutation()
    {
        var mainWindowSource = File.ReadAllText(GetMainWindowSourcePath());
        var stateSource = File.ReadAllText(GetStateSourcePath());

        mainWindowSource.Should().Contain("private void WarmupRollCallData()");
        mainWindowSource.Should().Contain("EnsureRollCallWindow();");
        mainWindowSource.Should().Contain("ExecuteLifecycleSafe(phase, \"close-rollcall-window\", rollCallWindow.RequestClose);");

        var guardIndex = stateSource.IndexOf("if (!_settingsSnapshotApplied)", StringComparison.Ordinal);
        var captureBoundsIndex = stateSource.IndexOf("CaptureWindowBounds();", StringComparison.Ordinal);
        var applyPatchIndex = stateSource.IndexOf("RollCallSettingsApplier.Apply(_settings, BuildPatchFromViewModel());", StringComparison.Ordinal);

        guardIndex.Should().BeGreaterThan(0);
        captureBoundsIndex.Should().BeGreaterThan(0);
        applyPatchIndex.Should().BeGreaterThan(0);
        guardIndex.Should().BeLessThan(captureBoundsIndex);
        guardIndex.Should().BeLessThan(applyPatchIndex);
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.xaml.cs");
    }

    private static string GetStateSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.State.cs");
    }

    private static string GetMainWindowSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Lifecycle.cs");
    }
}
