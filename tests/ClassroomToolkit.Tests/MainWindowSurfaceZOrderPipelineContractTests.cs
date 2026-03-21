using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowSurfaceZOrderPipelineContractTests
{
    [Fact]
    public void ApplySurfaceZOrderDecision_ShouldResolveDedupIntervalFromMainWindowPolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var interactionState = CaptureOverlayInteractionState();");
        source.Should().Contain("var dedupIntervalMs = MainWindowZOrderDedupIntervalPolicy.ResolveSurfaceDecisionIntervalMs(interactionState);");
    }

    [Fact]
    public void ApplySurfaceZOrderDecision_ShouldApplyDedupPolicyAndStateUpdater()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var dedupDecision = SurfaceZOrderDecisionDedupPolicy.Resolve(");
        source.Should().Contain("SurfaceZOrderDecisionStateUpdater.Apply(");
        source.Should().Contain("if (!dedupDecision.ShouldApply)");
    }

    [Fact]
    public void ApplySurfaceZOrderDecision_ShouldRouteFinalApplyThroughCoordinator()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("SurfaceZOrderCoordinator.Apply(");
        source.Should().Contain("surface => _windowOrchestrator.TouchSurface(_surfaceStack, surface),");
        source.Should().Contain("RequestApplyZOrderPolicy);");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
