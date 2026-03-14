using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class OverlayFocusExecutionExecutorTests
{
    [Fact]
    public void Resolve_ShouldReturnInputFlags()
    {
        var decision = OverlayFocusExecutionExecutor.Resolve(
            shouldActivate: true,
            shouldKeyboardFocus: false);

        decision.ShouldActivate.Should().BeTrue();
        decision.ShouldKeyboardFocus.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldInvokeActivateAndKeyboardFocus_WithRequestedFlags()
    {
        var activateCalled = false;
        var keyboardFocusCalled = false;

        OverlayFocusExecutionExecutor.Apply(
            target: "overlay",
            shouldActivate: true,
            shouldKeyboardFocus: true,
            tryActivate: (target, shouldActivate) =>
            {
                target.Should().Be("overlay");
                shouldActivate.Should().BeTrue();
                activateCalled = true;
                return true;
            },
            tryKeyboardFocus: (target, shouldFocus) =>
            {
                target.Should().Be("overlay");
                shouldFocus.Should().BeTrue();
                keyboardFocusCalled = true;
                return true;
            });

        activateCalled.Should().BeTrue();
        keyboardFocusCalled.Should().BeTrue();
    }

    [Fact]
    public void Apply_ShouldPassFalseFlags_WithoutSkippingExecutorCalls()
    {
        var activateFlag = true;
        var keyboardFocusFlag = true;

        OverlayFocusExecutionExecutor.Apply(
            target: "overlay",
            shouldActivate: false,
            shouldKeyboardFocus: false,
            tryActivate: (_, shouldActivate) =>
            {
                activateFlag = shouldActivate;
                return false;
            },
            tryKeyboardFocus: (_, shouldFocus) =>
            {
                keyboardFocusFlag = shouldFocus;
                return false;
            });

        activateFlag.Should().BeFalse();
        keyboardFocusFlag.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldForwardNullTarget_ToDelegates()
    {
        string? activateTarget = "non-null";
        string? focusTarget = "non-null";

        OverlayFocusExecutionExecutor.Apply<string>(
            target: null,
            shouldActivate: true,
            shouldKeyboardFocus: false,
            tryActivate: (target, _) =>
            {
                activateTarget = target;
                return false;
            },
            tryKeyboardFocus: (target, _) =>
            {
                focusTarget = target;
                return false;
            });

        activateTarget.Should().BeNull();
        focusTarget.Should().BeNull();
    }
}
