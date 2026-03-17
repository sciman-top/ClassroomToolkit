using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStateTransitionExecutorTests
{
    [Fact]
    public void Apply_ShouldThrowArgumentNullException_WhenApplyStateIsNull()
    {
        var act = () => WindowStateTransitionExecutor.Apply(
            target: "window",
            targetState: WindowState.Maximized,
            applyState: null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Apply_ShouldAssignRequestedState_WhenTargetExists()
    {
        var state = WindowState.Minimized;

        var applied = WindowStateTransitionExecutor.Apply(
            target: "window",
            targetState: WindowState.Maximized,
            applyState: (_, requestedState) =>
            {
                state = requestedState;
                return true;
            });

        applied.Should().BeTrue();
        state.Should().Be(WindowState.Maximized);
    }

    [Fact]
    public void Apply_ShouldReturnFalse_WhenTargetIsNull()
    {
        var applied = WindowStateTransitionExecutor.Apply<string>(
            target: null,
            targetState: WindowState.Normal,
            applyState: (_, _) => true);

        applied.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldReturnFalse_WhenApplyStateThrowsNonFatal()
    {
        var applied = WindowStateTransitionExecutor.Apply(
            target: "window",
            targetState: WindowState.Maximized,
            applyState: (_, _) => throw new InvalidOperationException("transition-failed"));

        applied.Should().BeFalse();
    }
}
