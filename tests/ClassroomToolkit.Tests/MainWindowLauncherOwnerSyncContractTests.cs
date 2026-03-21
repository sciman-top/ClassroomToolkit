using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowLauncherOwnerSyncContractTests
{
    [Fact]
    public void ResolveLauncherWindow_ShouldRouteThroughResolverAndResolutionPolicies()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var resolvedKind = LauncherWindowResolverPolicy.Resolve(");
        source.Should().Contain("return LauncherWindowResolutionPolicy.ResolveWindow(");
    }

    [Fact]
    public void CaptureLauncherSnapshot_ShouldUpdateVisibilityTimestampAndSelectionLogGate()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var snapshot = LauncherWindowRuntimeSnapshotPolicy.Resolve(");
        source.Should().Contain("LauncherTopmostVisibilityStateUpdater.ApplyResolvedTimestamp(");
        source.Should().Contain("if (LauncherWindowRuntimeSelectionLogPolicy.ShouldLog(snapshot.SelectionReason))");
    }

    [Fact]
    public void OverlayOwnerSync_ShouldRouteThroughSingleOwnerExecutionPolicyAndExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var action = FloatingSingleOwnerExecutionPolicy.Resolve(");
        source.Should().Contain("FloatingSingleOwnerExecutionExecutor.Apply(action, child, overlay);");
    }

    [Fact]
    public void FloatingOwnerSync_ShouldRouteThroughSnapshotPlanAndExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var snapshot = FloatingOwnerRuntimeSnapshotPolicy.Resolve(");
        source.Should().Contain("var plan = FloatingOwnerExecutionPlanPolicy.Resolve(snapshot);");
        source.Should().Contain("FloatingOwnerExecutionExecutor.Apply(");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
