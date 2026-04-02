using ClassroomToolkit.App;
using FluentAssertions;

namespace ClassroomToolkit.Tests;

public sealed class MainWindowExitPlanPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNoopPlan_WhenAlreadyAllowClose()
    {
        var plan = MainWindowExitPlanPolicy.Resolve(
            allowClose: true,
            backgroundTasksCancellationRequested: false,
            hasBubbleWindow: true,
            hasRollCallWindow: true,
            hasImageManagerWindow: true);

        plan.ShouldExit.Should().BeFalse();
        plan.ShouldCancelBackgroundTasks.Should().BeFalse();
        plan.ShouldCloseBubbleWindow.Should().BeFalse();
        plan.ShouldCloseRollCallWindow.Should().BeFalse();
        plan.ShouldCloseImageManagerWindow.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCancelBackgroundTasks_WhenNotCancelledYet()
    {
        var plan = MainWindowExitPlanPolicy.Resolve(
            allowClose: false,
            backgroundTasksCancellationRequested: false,
            hasBubbleWindow: false,
            hasRollCallWindow: false,
            hasImageManagerWindow: false);

        plan.ShouldExit.Should().BeTrue();
        plan.ShouldCancelBackgroundTasks.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldCloseExistingWindowsOnly()
    {
        var plan = MainWindowExitPlanPolicy.Resolve(
            allowClose: false,
            backgroundTasksCancellationRequested: true,
            hasBubbleWindow: true,
            hasRollCallWindow: false,
            hasImageManagerWindow: true);

        plan.ShouldExit.Should().BeTrue();
        plan.ShouldCancelBackgroundTasks.Should().BeFalse();
        plan.ShouldCloseBubbleWindow.Should().BeTrue();
        plan.ShouldCloseRollCallWindow.Should().BeFalse();
        plan.ShouldCloseImageManagerWindow.Should().BeTrue();
    }
}
