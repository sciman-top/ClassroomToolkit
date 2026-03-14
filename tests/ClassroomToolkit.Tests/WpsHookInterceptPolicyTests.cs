using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class WpsHookInterceptPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableKeyboardFallback_InCursorMode_WhenTargetNotForeground()
    {
        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Cursor,
            targetIsSlideshow: true,
            targetForeground: false,
            isRawSendMode: false,
            wheelForward: true);

        decision.InterceptKeyboard.Should().BeTrue();
        decision.InterceptWheel.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableInterception_InCursorMode_WhenTargetForeground()
    {
        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Cursor,
            targetIsSlideshow: true,
            targetForeground: true,
            isRawSendMode: false,
            wheelForward: true);

        decision.InterceptKeyboard.Should().BeFalse();
        decision.InterceptWheel.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepInterception_InDrawSlideshow_WhenTargetNotForeground()
    {
        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Brush,
            targetIsSlideshow: true,
            targetForeground: false,
            isRawSendMode: false,
            wheelForward: true);

        decision.InterceptKeyboard.Should().BeTrue();
        decision.InterceptWheel.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldDisableInterception_WhenTargetNotForeground_AndNotSlideshow()
    {
        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Brush,
            targetIsSlideshow: false,
            targetForeground: false,
            isRawSendMode: false,
            wheelForward: true);

        decision.InterceptKeyboard.Should().BeFalse();
        decision.InterceptWheel.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldNotUseBlockOnly_InDrawRawMode()
    {
        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Brush,
            targetIsSlideshow: true,
            targetForeground: false,
            isRawSendMode: true,
            wheelForward: true);

        decision.BlockOnly.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableWheelIntercept_WhenWheelForwardDisabled()
    {
        var decision = WpsHookInterceptPolicy.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Brush,
            targetIsSlideshow: true,
            targetForeground: true,
            isRawSendMode: false,
            wheelForward: false);

        decision.InterceptKeyboard.Should().BeTrue();
        decision.InterceptWheel.Should().BeFalse();
        decision.EmitWheelOnBlock.Should().BeFalse();
    }
}
