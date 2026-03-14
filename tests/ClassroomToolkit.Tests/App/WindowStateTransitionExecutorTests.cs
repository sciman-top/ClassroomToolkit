using System.Windows;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class WindowStateTransitionExecutorTests
{
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
}
