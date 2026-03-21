using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookKeyboardDispatchPolicyTests
{
    [Fact]
    public void ShouldDispatchFromHook_ShouldReturnFalse_WhenCursorKeyboardAndTargetForegroundOutsideProcess()
    {
        var shouldDispatch = WpsHookKeyboardDispatchPolicy.ShouldDispatchFromHook(
            source: "keyboard",
            mode: PaintToolMode.Cursor,
            targetForeground: true,
            foregroundOwnedByCurrentProcess: false);

        shouldDispatch.Should().BeFalse();
    }

    [Fact]
    public void ShouldDispatchFromHook_ShouldReturnTrue_WhenCursorKeyboardAndForegroundOwnedByCurrentProcess()
    {
        var shouldDispatch = WpsHookKeyboardDispatchPolicy.ShouldDispatchFromHook(
            source: "keyboard",
            mode: PaintToolMode.Cursor,
            targetForeground: true,
            foregroundOwnedByCurrentProcess: true);

        shouldDispatch.Should().BeTrue();
    }

    [Fact]
    public void ShouldDispatchFromHook_ShouldReturnTrue_WhenTargetNotForeground()
    {
        var shouldDispatch = WpsHookKeyboardDispatchPolicy.ShouldDispatchFromHook(
            source: "keyboard",
            mode: PaintToolMode.Cursor,
            targetForeground: false,
            foregroundOwnedByCurrentProcess: true);

        shouldDispatch.Should().BeTrue();
    }

    [Fact]
    public void ShouldDispatchFromHook_ShouldReturnTrue_ForWheelSource()
    {
        var shouldDispatch = WpsHookKeyboardDispatchPolicy.ShouldDispatchFromHook(
            source: "wheel",
            mode: PaintToolMode.Cursor,
            targetForeground: true,
            foregroundOwnedByCurrentProcess: false);

        shouldDispatch.Should().BeTrue();
    }
}
