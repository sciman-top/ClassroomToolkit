using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowRollCallTransitionContractTests
{
    [Fact]
    public void RollCallToggle_ShouldResolveTransitionPlan_FromVisibilityPolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var transitionPlan = RollCallVisibilityTransitionPolicy.Resolve(");
        source.Should().Contain("CaptureRollCallVisibilityTransitionContext()");
        source.Should().Contain("ApplyRollCallTransition(transitionPlan);");
    }

    [Fact]
    public void RollCallTransition_ShouldRouteActivationAndZOrder_ThroughExecutors()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("UserInitiatedWindowExecutionExecutor.Apply(");
        source.Should().Contain("_rollCallWindow,");
        source.Should().Contain("transitionPlan.ActivateWindow);");
        source.Should().Contain("FloatingZOrderApplyExecutor.Apply(");
        source.Should().Contain("transitionPlan.RequestZOrderApply,");
        source.Should().Contain("transitionPlan.ForceEnforceZOrder,");
        source.Should().Contain("RequestApplyZOrderPolicy);");
    }

    [Fact]
    public void RollCallTransition_ShouldKeepHideOwnerSyncShowOrderedByPlan()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (transitionPlan.HideWindow)");
        source.Should().Contain("if (transitionPlan.SyncOwnerToOverlay)");
        source.Should().Contain("if (transitionPlan.ShowWindow)");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.xaml.cs");
    }
}
