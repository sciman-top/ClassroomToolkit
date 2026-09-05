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
        var mainWindowSource = MainWindowContractSourceReader.ReadCombinedSource();
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

    [Fact]
    public void GroupSwitch_ShouldUseDebouncedPersist_ToAvoidSynchronousSavePerClick()
    {
        var inputSource = File.ReadAllText(GetInputSourcePath());
        var stateSource = File.ReadAllText(GetStateSourcePath());

        inputSource.Should().Contain("PersistSettingsDebounced();");
        inputSource.Should().NotContain("PersistSettings();");

        var debouncedGuardIndex = stateSource.IndexOf("private void PersistSettingsDebounced()", StringComparison.Ordinal);
        var debouncedGuardAppliedIndex = stateSource.IndexOf(
            "Skip PersistSettingsDebounced because settings snapshot has not been applied yet.",
            StringComparison.Ordinal);
        debouncedGuardIndex.Should().BeGreaterThan(0);
        debouncedGuardAppliedIndex.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SynchronousPersist_ShouldCancelPendingDebounce_AndClosingShouldStopSettingsTimer()
    {
        var stateSource = File.ReadAllText(GetStateSourcePath());
        var windowingSource = File.ReadAllText(GetWindowingSourcePath());

        var syncPersistIndex = stateSource.IndexOf("private void PersistSettings()", StringComparison.Ordinal);
        var persistBodyStart = stateSource.IndexOf('{', syncPersistIndex);
        var cancelTimerIndex = stateSource.IndexOf(
            "_settingsSaveTimer.Stop();",
            persistBodyStart + 1,
            StringComparison.Ordinal);
        var cancelDirtyIndex = stateSource.IndexOf(
            "_settingsSaveDirty = false;",
            persistBodyStart + 1,
            StringComparison.Ordinal);
        syncPersistIndex.Should().BeGreaterThan(0);
        persistBodyStart.Should().BeGreaterThan(syncPersistIndex);
        cancelTimerIndex.Should().BeGreaterThan(syncPersistIndex);
        cancelDirtyIndex.Should().BeGreaterThan(syncPersistIndex);

        windowingSource.Should().Contain("_settingsSaveTimer.Stop();");
        windowingSource.Should().Contain("_settingsSaveTimer.Tick -= OnSettingsSaveTick;");
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

    private static string GetInputSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Input.cs");
    }

    private static string GetWindowingSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "RollCallWindow.Windowing.cs");
    }
}
