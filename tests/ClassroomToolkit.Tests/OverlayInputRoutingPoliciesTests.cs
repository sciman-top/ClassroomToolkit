using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using ClassroomToolkit.App.Session;
using ClassroomToolkit.Interop;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class DispatcherInvokeAvailabilityPolicyTests
{
    [Theory]
    [InlineData(false, false, true)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    public void CanBeginInvoke_ShouldMatchExpected(
        bool hasShutdownStarted,
        bool hasShutdownFinished,
        bool expected)
    {
        OverlayInputRoutingPolicies.CanBeginInvoke(
                hasShutdownStarted,
                hasShutdownFinished)
            .Should()
            .Be(expected);
    }
}

public sealed class OverlayFocusAcceptancePolicyTests
{
    [Fact]
    public void ShouldBlockFocus_ShouldReturnTrue_WhenCursorPassthroughEnabled()
    {
        var result = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.Disabled,
            inputPassthroughEnabled: true,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: false,
            presentationTargetValid: false,
            wpsRawTargetValid: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenPhotoOrBoardActive()
    {
        var photo = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: true,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: false);

        var board = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: true,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: false);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenNavigationModeNotHybridOrHook()
    {
        var result = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.MessageOnly,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnTrue_WhenHybridAndPresentationTargetAvailable()
    {
        var result = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: false,
            wpsRawTargetValid: true);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenModeIsDraw()
    {
        var result = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Brush,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: true,
            presentationTargetValid: true,
            wpsRawTargetValid: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldBlockFocus_ShouldReturnFalse_WhenPresentationNotAllowed()
    {
        var result = OverlayInputRoutingPolicies.ShouldBlockFocus(
            UiNavigationMode.Hybrid,
            inputPassthroughEnabled: false,
            mode: PaintToolMode.Cursor,
            photoModeActive: false,
            boardActive: false,
            presentationAllowed: false,
            presentationTargetValid: true,
            wpsRawTargetValid: true);

        result.Should().BeFalse();
    }
}

public sealed class OverlayFocusResolverGatePolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public void ShouldResolvePresentationTarget_ShouldMatchExpected(
        bool presentationAllowed,
        bool navigationAllowsPresentationInput,
        bool expected)
    {
        OverlayInputRoutingPolicies.ShouldResolvePresentationTarget(
                presentationAllowed,
                navigationAllowsPresentationInput)
            .Should()
            .Be(expected);
    }
}

public sealed class OverlayHitTestPolicyTests
{
    [Theory]
    [InlineData(PaintToolMode.Cursor, false, false, false)]
    [InlineData(PaintToolMode.Cursor, true, false, true)]
    [InlineData(PaintToolMode.Brush, false, false, true)]
    [InlineData(PaintToolMode.Brush, true, false, true)]
    [InlineData(PaintToolMode.Cursor, true, true, false)]
    [InlineData(PaintToolMode.Brush, true, true, false)]
    public void ShouldEnableOverlayHitTest_ShouldMatchExpected(
        PaintToolMode mode,
        bool photoModeActive,
        bool photoLoading,
        bool expected)
    {
        var enabled = OverlayInputRoutingPolicies.ShouldEnableOverlayHitTest(
            mode,
            photoModeActive,
            photoLoading);

        enabled.Should().Be(expected);
    }
}

public sealed class OverlayInputPassthroughPolicyTests
{
    [Theory]
    [InlineData(PaintToolMode.Cursor, 0.0, false, true)]
    [InlineData(PaintToolMode.Cursor, 0.0005, false, true)]
    [InlineData(PaintToolMode.Cursor, 0.01, false, false)]
    [InlineData(PaintToolMode.Brush, 0.0, false, false)]
    [InlineData(PaintToolMode.Cursor, 0.0, true, false)]
    public void ShouldEnable_ShouldFollowCursorBoardPhotoGuards(
        PaintToolMode mode,
        double boardOpacity,
        bool photoModeActive,
        bool expected)
    {
        var enabled = OverlayInputRoutingPolicies.ShouldEnable(mode, boardOpacity, photoModeActive);

        enabled.Should().Be(expected);
    }
}

public sealed class OverlayInputRoutingPolicyTests
{
    [Theory]
    [InlineData(true, false, true, true, (int)OverlayWheelInputRoute.ConsumeForBoard)]
    [InlineData(false, true, true, true, (int)OverlayWheelInputRoute.HandlePhoto)]
    [InlineData(false, false, false, true, (int)OverlayWheelInputRoute.Ignore)]
    [InlineData(false, false, true, false, (int)OverlayWheelInputRoute.Ignore)]
    [InlineData(false, false, true, true, (int)OverlayWheelInputRoute.RoutePresentation)]
    public void ResolveWheelRoute_ShouldMatchExpected(
        bool boardActive,
        bool photoModeActive,
        bool canRoutePresentationInput,
        bool presentationChannelEnabled,
        int expectedValue)
    {
        var expected = (OverlayWheelInputRoute)expectedValue;
        var route = OverlayInputRoutingPolicies.ResolveWheelRoute(
            boardActive,
            photoModeActive,
            canRoutePresentationInput,
            presentationChannelEnabled);

        route.Should().Be(expected);
    }

    [Theory]
    [InlineData(true, false, false, true, (int)OverlayKeyInputRoute.Consume)]
    [InlineData(false, true, false, true, (int)OverlayKeyInputRoute.Consume)]
    [InlineData(false, false, true, true, (int)OverlayKeyInputRoute.Ignore)]
    [InlineData(false, false, false, false, (int)OverlayKeyInputRoute.Ignore)]
    [InlineData(false, false, false, true, (int)OverlayKeyInputRoute.RoutePresentation)]
    public void ResolveKeyRoute_ShouldMatchExpected(
        bool photoLoading,
        bool photoKeyHandled,
        bool photoOrBoardActive,
        bool canRoutePresentationInput,
        int expectedValue)
    {
        var expected = (OverlayKeyInputRoute)expectedValue;
        var route = OverlayInputRoutingPolicies.ResolveKeyRoute(
            photoLoading,
            photoKeyHandled,
            photoOrBoardActive,
            canRoutePresentationInput);

        route.Should().Be(expected);
    }
}

public sealed class OverlayPointerSourceGatePolicyTests
{
    [Theory]
    [InlineData(true, false, (int)OverlayPointerSourceGateDecision.Consume)]
    [InlineData(false, true, (int)OverlayPointerSourceGateDecision.Ignore)]
    [InlineData(false, false, (int)OverlayPointerSourceGateDecision.Continue)]
    public void Resolve_ShouldMatchExpected(
        bool photoLoading,
        bool ignoreFromPhotoControls,
        int expectedValue)
    {
        var expected = (OverlayPointerSourceGateDecision)expectedValue;
        var decision = OverlayInputRoutingPolicies.ResolveOverlayPointerSourceGate(
            photoLoading,
            ignoreFromPhotoControls);

        decision.Should().Be(expected);
    }
}

public sealed class OverlayPointerSourceHandlingPolicyTests
{
    [Fact]
    public void Resolve_ShouldAllowContinue_WhenGateAllowsContinue()
    {
        var plan = OverlayInputRoutingPolicies.ResolveOverlayPointerSourceHandling(
            OverlayPointerSourceGateDecision.Continue,
            hideEraserPreviewWhenBlocked: true);

        plan.Should().Be(new OverlayPointerSourceHandlingPlan(
            ShouldContinue: true,
            ShouldMarkHandled: false,
            ShouldHideEraserPreview: false));
    }

    [Fact]
    public void Resolve_ShouldConsumeAndHide_WhenGateConsumesAndHideRequested()
    {
        var plan = OverlayInputRoutingPolicies.ResolveOverlayPointerSourceHandling(
            OverlayPointerSourceGateDecision.Consume,
            hideEraserPreviewWhenBlocked: true);

        plan.Should().Be(new OverlayPointerSourceHandlingPlan(
            ShouldContinue: false,
            ShouldMarkHandled: true,
            ShouldHideEraserPreview: true));
    }

    [Fact]
    public void Resolve_ShouldIgnoreWithoutHandle_WhenGateIgnores()
    {
        var plan = OverlayInputRoutingPolicies.ResolveOverlayPointerSourceHandling(
            OverlayPointerSourceGateDecision.Ignore,
            hideEraserPreviewWhenBlocked: false);

        plan.Should().Be(new OverlayPointerSourceHandlingPlan(
            ShouldContinue: false,
            ShouldMarkHandled: false,
            ShouldHideEraserPreview: false));
    }
}

public sealed class OverlayPresentationRoutingPolicyTests
{
    [Fact]
    public void CanRouteFromAuxWindow_ShouldAllow_WhenPresentationInputAllowed_AndNotPhotoBoard()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromAuxWindow(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromAuxWindow_ShouldDeny_WhenNavigationDisabled()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromAuxWindow(
            UiNavigationMode.Disabled,
            photoModeActive: false,
            boardActive: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromAuxWindow_ShouldDeny_WhenPhotoOrBoardActive()
    {
        var photo = OverlayInputRoutingPolicies.CanRouteFromAuxWindow(
            UiNavigationMode.Hybrid,
            photoModeActive: true,
            boardActive: false);
        var board = OverlayInputRoutingPolicies.CanRouteFromAuxWindow(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: true);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenHybridAndNotPhotoBoard()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenHookOnlyAndNotPhotoBoard()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.HookOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenDrawModeAndHookOnly()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.HookOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenNavigationDisabled()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.Disabled,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inputPassthroughEnabled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenDrawModeButNotHookOrHybrid()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.MessageOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Brush,
            inputPassthroughEnabled: false);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenPhotoOrBoardActive()
    {
        var photo = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: true,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);
        var board = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: true,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        photo.Should().BeFalse();
        board.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenCursorPassthroughEnabled()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.Hybrid,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldAllow_WhenCursorAndHookOnly()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.HookOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeTrue();
    }

    [Fact]
    public void CanRouteFromOverlay_ShouldDeny_WhenCursorAndMessageOnly()
    {
        var result = OverlayInputRoutingPolicies.CanRouteFromOverlay(
            UiNavigationMode.MessageOnly,
            photoModeActive: false,
            boardActive: false,
            mode: PaintToolMode.Cursor,
            inputPassthroughEnabled: false);

        result.Should().BeFalse();
    }
}

public sealed class OverlayTopmostApplyGatePolicyTests
{
    [Theory]
    [InlineData(false, WindowState.Normal, false)]
    [InlineData(true, WindowState.Minimized, false)]
    [InlineData(true, WindowState.Normal, true)]
    [InlineData(true, WindowState.Maximized, true)]
    public void ShouldApply_ShouldMatchExpected(bool overlayVisible, WindowState windowState, bool expected)
    {
        OverlayInputRoutingPolicies.ShouldApplyOverlayTopmostApplyGate(overlayVisible, windowState)
            .Should()
            .Be(expected);
    }
}

public sealed class OverlayWheelPresentationExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenWpsHookBlocksDirectSend()
    {
        var action = OverlayInputRoutingPolicies.ResolveOverlayWheelPresentationExecution(
            hookActive: true,
            hookInterceptWheel: true,
            hookBlockOnly: true,
            isWpsForeground: true,
            hookRecentlyFired: false,
            wheelDelta: -120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.None);
    }

    [Fact]
    public void Resolve_ShouldReturnNone_WhenHookRecentlyFired()
    {
        var action = OverlayInputRoutingPolicies.ResolveOverlayWheelPresentationExecution(
            hookActive: false,
            hookInterceptWheel: false,
            hookBlockOnly: false,
            isWpsForeground: false,
            hookRecentlyFired: true,
            wheelDelta: -120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.None);
    }

    [Fact]
    public void Resolve_ShouldReturnSendNext_WhenDeltaNegative()
    {
        var action = OverlayInputRoutingPolicies.ResolveOverlayWheelPresentationExecution(
            hookActive: false,
            hookInterceptWheel: false,
            hookBlockOnly: false,
            isWpsForeground: false,
            hookRecentlyFired: false,
            wheelDelta: -120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.SendNext);
    }

    [Fact]
    public void Resolve_ShouldReturnSendPrevious_WhenDeltaNonNegative()
    {
        var action = OverlayInputRoutingPolicies.ResolveOverlayWheelPresentationExecution(
            hookActive: false,
            hookInterceptWheel: false,
            hookBlockOnly: false,
            isWpsForeground: false,
            hookRecentlyFired: false,
            wheelDelta: 120);

        action.Should().Be(OverlayWheelPresentationExecutionAction.SendPrevious);
    }
}

public sealed class OverlayWindowStyleApplyPolicyTests
{
    [Fact]
    public void ShouldApply_ShouldReturnTrue_WhenNoPreviousState()
    {
        var shouldApply = OverlayInputRoutingPolicies.ShouldApplyOverlayWindowStyleApply(
            inputPassthroughEnabled: true,
            focusBlocked: false,
            lastInputPassthroughEnabled: null,
            lastFocusBlocked: null);

        shouldApply.Should().BeTrue();
    }

    [Fact]
    public void ShouldApply_ShouldReturnFalse_WhenStateUnchanged()
    {
        var shouldApply = OverlayInputRoutingPolicies.ShouldApplyOverlayWindowStyleApply(
            inputPassthroughEnabled: true,
            focusBlocked: false,
            lastInputPassthroughEnabled: true,
            lastFocusBlocked: false);

        shouldApply.Should().BeFalse();
    }

    [Fact]
    public void ShouldApply_ShouldReturnTrue_WhenAnyFlagChanged()
    {
        var shouldApply = OverlayInputRoutingPolicies.ShouldApplyOverlayWindowStyleApply(
            inputPassthroughEnabled: false,
            focusBlocked: false,
            lastInputPassthroughEnabled: true,
            lastFocusBlocked: false);

        shouldApply.Should().BeTrue();
    }
}

public sealed class OverlayWindowStyleBitsPolicyTests
{
    [Fact]
    public void Resolve_ShouldSetTransparentAndNoActivate_WhenBothEnabled()
    {
        var mask = OverlayInputRoutingPolicies.ResolveOverlayWindowStyleBits(
            inputPassthroughEnabled: true,
            focusBlocked: true);

        mask.SetMask.Should().Be(NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate);
        mask.ClearMask.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldClearBoth_WhenBothDisabled()
    {
        var mask = OverlayInputRoutingPolicies.ResolveOverlayWindowStyleBits(
            inputPassthroughEnabled: false,
            focusBlocked: false);

        mask.SetMask.Should().Be(0);
        mask.ClearMask.Should().Be(NativeMethods.WsExTransparent | NativeMethods.WsExNoActivate);
    }
}

public sealed class PresentationChannelAvailabilityPolicyTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    public void IsAnyChannelEnabled_ShouldMatchExpected(bool allowOffice, bool allowWps, bool expected)
    {
        OverlayInputRoutingPolicies.IsAnyChannelEnabled(allowOffice, allowWps)
            .Should()
            .Be(expected);
    }
}
