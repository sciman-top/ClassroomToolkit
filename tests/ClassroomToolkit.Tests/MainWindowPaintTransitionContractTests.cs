using System.IO;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowPaintTransitionContractTests
{
    [Fact]
    public void PaintTogglePath_ShouldResolveTransitionPlanFromPolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var transitionPlan = PaintVisibilityTransitionPolicy.ResolvePaintToggle(overlay.IsVisible);");
        source.Should().Contain("ApplyPaintToggleTransition(transitionPlan);");
    }

    [Fact]
    public void EnsureOverlayVisiblePath_ShouldResolveTransitionPlanFromPolicy()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("var transitionPlan = PaintVisibilityTransitionPolicy.ResolveEnsureOverlayVisible(");
        source.Should().Contain("ApplyEnsurePaintOverlayVisibleTransition(transitionPlan);");
    }

    [Fact]
    public void PaintTransitionApply_ShouldRouteZOrderThroughFloatingExecutor()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("FloatingZOrderApplyExecutor.Apply(");
        source.Should().Contain("transitionPlan.RequestZOrderApply,");
        source.Should().Contain("transitionPlan.ForceEnforceZOrder,");
        source.Should().Contain("RequestApplyZOrderPolicy);");
    }

    [Fact]
    public void EnsurePaintWindows_ShouldKeepSkipAndCreationPolicies()
    {
        var source = File.ReadAllText(GetSourcePath());

        source.Should().Contain("if (PaintWindowEnsureSkipPolicy.ShouldSkip(");
        source.Should().Contain("if (PaintWindowCreationPolicy.ShouldEnsureWindows(");
    }

    private static string GetSourcePath()
    {
        return TestPathHelper.ResolveRepoPath(
            "src",
            "ClassroomToolkit.App",
            "MainWindow.Paint.cs");
    }
}
