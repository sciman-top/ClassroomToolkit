using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.Interop.Presentation;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class WpsFullscreenExitPolicyTests
{
    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldReturnFalse_WhenNoFullscreenCandidate()
    {
        var active = WpsHookPolicies.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: false,
            foregroundType: PresentationType.Wps,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: false);

        active.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldReturnFalse_WhenForegroundReturnedToNonFullscreenWps()
    {
        var active = WpsHookPolicies.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: true,
            foregroundType: PresentationType.Wps,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: false);

        active.Should().BeFalse();
    }

    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldKeepFullscreen_WhenOverlayOwnsForeground()
    {
        var active = WpsHookPolicies.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: true,
            foregroundType: PresentationType.None,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: true);

        active.Should().BeTrue();
    }

    [Fact]
    public void ShouldTreatAsActiveFullscreen_ShouldKeepFullscreen_WhenUnrelatedWindowOwnsForeground()
    {
        var active = WpsHookPolicies.ShouldTreatAsActiveFullscreen(
            hasFullscreenCandidate: true,
            foregroundType: PresentationType.Other,
            foregroundIsFullscreen: false,
            foregroundOwnedByCurrentProcess: false);

        active.Should().BeTrue();
    }
}

public sealed class WpsHookEnableGatePolicyTests
{
    [Fact]
    public void ShouldAttemptResolveTarget_ShouldReturnFalse_WhenRuntimeGateNotPassed()
    {
        WpsHookPolicies.ShouldAttemptResolveTarget(
                allowWps: false,
                boardActive: false,
                overlayVisible: true,
                photoModeActive: false)
            .Should()
            .BeFalse();

        WpsHookPolicies.ShouldAttemptResolveTarget(
                allowWps: true,
                boardActive: true,
                overlayVisible: true,
                photoModeActive: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldAttemptResolveTarget_ShouldReturnTrue_WhenRuntimeGatePassed()
    {
        WpsHookPolicies.ShouldAttemptResolveTarget(
                allowWps: true,
                boardActive: false,
                overlayVisible: true,
                photoModeActive: false)
            .Should()
            .BeTrue();
    }

    [Theory]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void ShouldEnableWithTarget_ShouldMatchExpected(
        bool shouldAttemptResolveTarget,
        bool targetValid,
        bool targetIsSlideshow,
        bool expected)
    {
        WpsHookPolicies.ShouldEnableWithTarget(
                shouldAttemptResolveTarget,
                targetValid,
                targetIsSlideshow)
            .Should()
            .Be(expected);
    }
}

public sealed class WpsHookInputDebouncePolicyTests
{
    [Fact]
    public void IsRecent_ShouldReturnFalse_WhenNotInitialized()
    {
        var nowUtc = new DateTime(2026, 3, 7, 5, 0, 0, DateTimeKind.Utc);

        var recent = WpsHookPolicies.IsRecent(
            PresentationRuntimeDefaults.UnsetTimestampUtc,
            nowUtc,
            debounceMs: 200);

        recent.Should().BeFalse();
    }

    [Fact]
    public void IsRecent_ShouldReturnTrue_WhenWithinDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 5, 0, 0, DateTimeKind.Utc);

        var recent = WpsHookPolicies.IsRecent(
            nowUtc.AddMilliseconds(-80),
            nowUtc,
            debounceMs: 200);

        recent.Should().BeTrue();
    }

    [Fact]
    public void IsRecent_ShouldReturnFalse_WhenOutsideDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 5, 0, 0, DateTimeKind.Utc);

        var recent = WpsHookPolicies.IsRecent(
            nowUtc.AddMilliseconds(-260),
            nowUtc,
            debounceMs: 200);

        recent.Should().BeFalse();
    }
}

public sealed class WpsHookInterceptPolicyTests
{
    [Fact]
    public void Resolve_ShouldEnableKeyboardFallback_InCursorMode_WhenTargetNotForeground()
    {
        var decision = WpsHookPolicies.Resolve(
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
        var decision = WpsHookPolicies.Resolve(
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
        var decision = WpsHookPolicies.Resolve(
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
        var decision = WpsHookPolicies.Resolve(
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
        var decision = WpsHookPolicies.Resolve(
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
        var decision = WpsHookPolicies.Resolve(
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

    [Fact]
    public void Resolve_ShouldKeepNativeWheel_WhenWpsTargetIsForeground()
    {
        var decision = WpsHookPolicies.Resolve(
            shouldEnable: true,
            mode: PaintToolMode.Brush,
            targetIsSlideshow: true,
            targetForeground: true,
            isRawSendMode: false,
            wheelForward: true);

        decision.InterceptWheel.Should().BeFalse();
        decision.EmitWheelOnBlock.Should().BeFalse();
    }
}

public sealed class WpsHookNavigationInjectionGatePolicyTests
{
    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenTargetIsForeground()
    {
        var suppressed = WpsHookPolicies.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundInputAuthorized: false,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 真实按键已直达前台放映窗，再注入必然双翻页。
        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForeignAppIsForeground()
    {
        var suppressed = WpsHookPolicies.ShouldSuppressInjection(
            targetIsForeground: false,
            foregroundInputAuthorized: false,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 外来应用前台的按键属于该应用，不得转译为放映翻页。
        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnFalse_WhenOwnProcessIsForeground()
    {
        var suppressed = WpsHookPolicies.ShouldSuppressInjection(
            targetIsForeground: false,
            foregroundInputAuthorized: true,
            wheelSource: false,
            wheelAsKeyEnabled: false);

        // 覆盖层/工具条持有前台时保留翻页笔中继。
        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForegroundWheelMappingWouldDuplicateNativeInput()
    {
        var suppressed = WpsHookPolicies.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundInputAuthorized: false,
            wheelSource: true,
            wheelAsKeyEnabled: true);

        // WheelAsKey 仅桥接后台目标；WPS 已前台时保留原生滚轮，避免双翻页。
        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppressInjection_ShouldReturnTrue_WhenForegroundKeyboardEvenWithWheelAsKey()
    {
        var suppressed = WpsHookPolicies.ShouldSuppressInjection(
            targetIsForeground: true,
            foregroundInputAuthorized: true,
            wheelSource: false,
            wheelAsKeyEnabled: true);

        suppressed.Should().BeTrue();
    }
}

public sealed class WpsHookUnavailableNotificationPolicyTests
{
    [Fact]
    public void ShouldNotify_ShouldReturnTrueOnlyFirstTime()
    {
        var state = 0;

        var first = WpsHookPolicies.ShouldNotify(ref state);
        var second = WpsHookPolicies.ShouldNotify(ref state);

        first.Should().BeTrue();
        second.Should().BeFalse();
        WpsHookPolicies.IsNotified(ref state).Should().BeTrue();
    }

    [Fact]
    public void Reset_ShouldAllowNotifyAgain()
    {
        var state = 0;
        _ = WpsHookPolicies.ShouldNotify(ref state);

        WpsHookPolicies.Reset(ref state);
        var afterReset = WpsHookPolicies.ShouldNotify(ref state);

        afterReset.Should().BeTrue();
    }
}

public sealed class WpsNavigationDebouncePolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenTargetIsZero()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: null);

        var suppressed = WpsHookPolicies.ShouldSuppress(
            direction: 1,
            target: IntPtr.Zero,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenNoLastEvent()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: null);

        var suppressed = WpsHookPolicies.ShouldSuppress(
            direction: 1,
            target: (IntPtr)123,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenSameTargetAndCodeWithinDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc.AddMilliseconds(-60)));

        var suppressed = WpsHookPolicies.ShouldSuppress(
            direction: 1,
            target: (IntPtr)123,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenOppositeDirectionWithinDebounceWindow()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc.AddMilliseconds(-60)));

        var suppressed = WpsHookPolicies.ShouldSuppress(
            direction: -1,
            target: (IntPtr)123,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenDifferentTarget()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc.AddMilliseconds(-60)));

        var suppressed = WpsHookPolicies.ShouldSuppress(
            direction: 1,
            target: (IntPtr)456,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenDebounceWindowElapsed()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);
        var state = new WpsNavigationDebounceState(
            LastEvent: (1, (IntPtr)123, nowUtc.AddMilliseconds(-200)));

        var suppressed = WpsHookPolicies.ShouldSuppress(
            direction: 1,
            target: (IntPtr)123,
            nowUtc: nowUtc,
            state: state,
            debounceMs: 200);

        suppressed.Should().BeFalse();
    }

    [Fact]
    public void Remember_ShouldCaptureEvent()
    {
        var nowUtc = new DateTime(2026, 3, 7, 3, 0, 0, DateTimeKind.Utc);

        var state = WpsHookPolicies.Remember(
            direction: -1,
            target: (IntPtr)999,
            nowUtc: nowUtc);

        state.LastEvent.Should().Be((-1, (IntPtr)999, nowUtc));
    }
}

public sealed class WpsPresentationRuntimePolicyTests
{
    [Theory]
    [InlineData("wpp.exe")]
    [InlineData("wppt")]
    [InlineData("WpsPresentationHost.exe")]
    public void IsDedicatedSlideshowRuntime_ShouldRecognizeDedicatedRuntimeProcesses(string processName)
    {
        var dedicated = WpsHookPolicies.IsDedicatedSlideshowRuntime(processName);

        dedicated.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("wps.exe")]
    [InlineData("powerpnt.exe")]
    [InlineData("explorer.exe")]
    public void IsDedicatedSlideshowRuntime_ShouldRejectEditorAndUnrelatedProcesses(string processName)
    {
        var dedicated = WpsHookPolicies.IsDedicatedSlideshowRuntime(processName);

        dedicated.Should().BeFalse();
    }
}

public sealed class WpsRawFallbackTargetPolicyTests
{
    [Theory]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    public void ShouldResolveWpsRawTarget_ShouldMatchExpected(
        bool presentationTargetValid,
        bool allowWps,
        bool expected)
    {
        WpsHookPolicies.ShouldResolveWpsRawTarget(presentationTargetValid, allowWps)
            .Should()
            .Be(expected);
    }

    [Theory]
    [InlineData(false, InputStrategy.Raw, false)]
    [InlineData(true, InputStrategy.Message, false)]
    [InlineData(true, InputStrategy.Raw, true)]
    public void IsValid_ShouldMatchExpected(bool wpsTargetValid, InputStrategy wpsSendMode, bool expected)
    {
        WpsHookPolicies.IsValid(wpsTargetValid, wpsSendMode)
            .Should()
            .Be(expected);
    }
}

public sealed class WpsWheelRoutingPolicyTests
{
    [Fact]
    public void ShouldBypassDirectSend_ShouldReturnTrue_WhenWpsHookIsBlocking()
    {
        var result = WpsHookPolicies.ShouldBypassDirectSend(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: true,
            isWpsForeground: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBypassDirectSend_ShouldReturnFalse_WhenHookNotBlocking()
    {
        var result = WpsHookPolicies.ShouldBypassDirectSend(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: false,
            isWpsForeground: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldBypassDirectSend_ShouldReturnFalse_WhenForegroundNotWps()
    {
        var result = WpsHookPolicies.ShouldBypassDirectSend(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: true,
            isWpsForeground: false);

        result.Should().BeFalse();
    }
}
