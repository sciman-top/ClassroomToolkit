using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageInputDisplayPolicyTests
{
    [Fact]
    public void IsActive_ShouldReturnTrue_WhenPhotoModeOnBoardOffAndCrossPageEnabled()
    {
        CrossPageInputSwitchPolicies.IsActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenBoardActive()
    {
        CrossPageInputSwitchPolicies.IsActive(
            photoModeActive: true,
            boardActive: true,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void IsActive_ShouldReturnFalse_WhenCrossPageDisabled()
    {
        CrossPageInputSwitchPolicies.IsActive(
            photoModeActive: true,
            boardActive: false,
            crossPageDisplayEnabled: false).Should().BeFalse();
    }
}

public sealed class CrossPageInputResumePolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnNone_WhenNotSwitched()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveCrossPageInputResume(
            switchedPage: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: false,
            isErasing: false,
            replayCurrentInput: false,
            hasPendingBrushSeed: false,
            pendingSeedEqualsInput: true);

        plan.Action.Should().Be(CrossPageInputResumeAction.None);
        plan.ShouldClearPendingBrushState.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false, true, false)]
    [InlineData(false, false, false, true)]
    [InlineData(false, true, false, true)]
    [InlineData(true, false, true, true)]
    [InlineData(true, true, false, true)]
    public void Resolve_ShouldReturnBrushContinuationPlan_WhenBrushAndNoStrokeInProgress(
        bool replayCurrentInput,
        bool hasPendingBrushSeed,
        bool pendingSeedEqualsInput,
        bool expectedShouldUpdate)
    {
        var plan = CrossPageInputSwitchPolicies.ResolveCrossPageInputResume(
            switchedPage: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: false,
            isErasing: false,
            replayCurrentInput: replayCurrentInput,
            hasPendingBrushSeed: hasPendingBrushSeed,
            pendingSeedEqualsInput: pendingSeedEqualsInput);

        plan.Action.Should().Be(CrossPageInputResumeAction.BeginBrushContinuation);
        plan.ShouldClearPendingBrushState.Should().BeTrue();
        plan.ShouldUpdateBrushAfterContinuation.Should().Be(expectedShouldUpdate);
    }

    [Fact]
    public void Resolve_ShouldReturnBeginEraser_WhenEraserAndNotErasing()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveCrossPageInputResume(
            switchedPage: true,
            mode: PaintToolMode.Eraser,
            strokeInProgress: false,
            isErasing: false,
            replayCurrentInput: false,
            hasPendingBrushSeed: false,
            pendingSeedEqualsInput: true);

        plan.Action.Should().Be(CrossPageInputResumeAction.BeginEraser);
        plan.ShouldClearPendingBrushState.Should().BeFalse();
    }
}

public sealed class CrossPageInputSwitchAdmissionPolicyTests
{
    [Theory]
    [InlineData(false, true, false, true, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, true, true, false, false)]
    [InlineData(true, true, false, false, true)]
    [InlineData(true, true, true, true, true)]
    public void ShouldProceed_ShouldMatchExpected(
        bool canSwitchByGate,
        bool hasBitmap,
        bool hasCurrentRect,
        bool shouldSwitchByPointer,
        bool expected)
    {
        var result = CrossPageInputSwitchPolicies.ShouldProceed(
            canSwitchByGate,
            hasBitmap,
            hasCurrentRect,
            shouldSwitchByPointer);

        result.Should().Be(expected);
    }
}

public sealed class CrossPageInputSwitchBounceGuardPolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenReverseSwitchHappensNearSeamWithinCooldown()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-40);

        var suppress = CrossPageInputSwitchPolicies.ShouldSuppress(
            currentPage: 6,
            targetPage: 5,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 504,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenReverseSwitchIsFarFromSeam()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-40);

        var suppress = CrossPageInputSwitchPolicies.ShouldSuppress(
            currentPage: 6,
            targetPage: 5,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 545,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenCooldownElapsed()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-120);

        var suppress = CrossPageInputSwitchPolicies.ShouldSuppress(
            currentPage: 6,
            targetPage: 5,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 504,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenSwitchIsNotReverseDirection()
    {
        var nowUtc = new DateTime(2026, 3, 15, 10, 0, 0, DateTimeKind.Utc);
        var lastSwitchUtc = nowUtc.AddMilliseconds(-40);

        var suppress = CrossPageInputSwitchPolicies.ShouldSuppress(
            currentPage: 6,
            targetPage: 7,
            lastSwitchFromPage: 5,
            lastSwitchToPage: 6,
            lastSwitchUtc: lastSwitchUtc,
            nowUtc: nowUtc,
            pointerY: 504,
            seamY: 500,
            seamBandDip: 18,
            cooldownMs: 90);

        suppress.Should().BeFalse();
    }
}

public sealed class CrossPageInputSwitchExecutionPolicyTests
{
    [Fact]
    public void Resolve_ShouldSkip_WhenTargetPageInvalid()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveExecution(
            currentPage: 3,
            targetPage: 3,
            mode: PaintToolMode.Brush,
            currentPageHeight: 800);

        plan.ShouldSwitch.Should().BeFalse();
        plan.ShouldResolveBrushContinuation.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldEnableBrushContinuation_WhenBrushAndPageHeightPositive()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveExecution(
            currentPage: 3,
            targetPage: 4,
            mode: PaintToolMode.Brush,
            currentPageHeight: 800);

        plan.ShouldSwitch.Should().BeTrue();
        plan.ShouldResolveBrushContinuation.Should().BeTrue();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDisableBrushContinuation_WhenBrushButPageHeightNonPositive()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveExecution(
            currentPage: 3,
            targetPage: 4,
            mode: PaintToolMode.Brush,
            currentPageHeight: 0);

        plan.ShouldSwitch.Should().BeTrue();
        plan.ShouldResolveBrushContinuation.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldDeferDisplayUpdate_WhenNotBrushMode()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveExecution(
            currentPage: 3,
            targetPage: 2,
            mode: PaintToolMode.Eraser,
            currentPageHeight: 600);

        plan.ShouldSwitch.Should().BeTrue();
        plan.ShouldResolveBrushContinuation.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeTrue();
    }
}

public sealed class CrossPageInputSwitchGatePolicyTests
{
    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenNotPhotoOrNotCrossPage()
    {
        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: false,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();

        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: false,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenToolUnsupported()
    {
        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Cursor,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenPanningOrDragging()
    {
        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: true,
                crossPageDragging: false)
            .Should()
            .BeFalse();

        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: true)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnTrue_WhenBrushOrEraserAndIdle()
    {
        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeTrue();

        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: false,
                mode: PaintToolMode.Eraser,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void CanSwitchForInput_ShouldReturnFalse_WhenWhiteboardActive()
    {
        CrossPageInputSwitchPolicies.CanSwitchForInput(
                photoModeActive: true,
                crossPageDisplayEnabled: true,
                boardActive: true,
                mode: PaintToolMode.Brush,
                photoPanning: false,
                crossPageDragging: false)
            .Should()
            .BeFalse();
    }
}

public sealed class CrossPageInputSwitchNavigationPolicyTests
{
    [Theory]
    [InlineData((int)PaintToolMode.Brush, true, false, false)]
    [InlineData((int)PaintToolMode.Eraser, false, true, false)]
    [InlineData((int)PaintToolMode.RegionErase, false, false, true)]
    public void Resolve_ShouldPreferInteractiveBrushPath_WhileMutatingInkAcrossPages(
        int modeValue,
        bool strokeInProgress,
        bool isErasing,
        bool isRegionSelecting)
    {
        var plan = CrossPageInputSwitchPolicies.ResolveNavigation(
            (PaintToolMode)modeValue,
            strokeInProgress,
            isErasing,
            isRegionSelecting);

        if ((PaintToolMode)modeValue == PaintToolMode.Brush)
        {
            plan.InteractiveSwitch.Should().BeTrue();
            plan.DeferCrossPageDisplayUpdate.Should().BeTrue();
        }
        else
        {
            plan.InteractiveSwitch.Should().BeFalse();
            plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
        }
    }

    [Theory]
    [InlineData((int)PaintToolMode.Brush, false, false, false)]
    [InlineData((int)PaintToolMode.Eraser, false, false, false)]
    [InlineData((int)PaintToolMode.Cursor, false, false, false)]
    public void Resolve_ShouldKeepInteractivePath_WhenNoActiveInkMutation(
        int modeValue,
        bool strokeInProgress,
        bool isErasing,
        bool isRegionSelecting)
    {
        var plan = CrossPageInputSwitchPolicies.ResolveNavigation(
            (PaintToolMode)modeValue,
            strokeInProgress,
            isErasing,
            isRegionSelecting);

        plan.InteractiveSwitch.Should().BeTrue();
        plan.DeferCrossPageDisplayUpdate.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnInteractivePath_WhenSwitchWasTriggeredByActiveBrushMutation()
    {
        var plan = CrossPageInputSwitchPolicies.ResolveNavigation(
            PaintToolMode.Brush,
            strokeInProgress: false,
            isErasing: false,
            isRegionSelecting: false,
            inputTriggeredByActiveInkMutation: true);

        plan.InteractiveSwitch.Should().BeTrue();
        plan.DeferCrossPageDisplayUpdate.Should().BeTrue();
    }
}

public sealed class CrossPageInputSwitchPolicyTests
{
    [Fact]
    public void ShouldSwitchByPointer_ShouldReturnFalse_WhenPointerInsideExpandedRect()
    {
        var rect = new Rect(100, 100, 300, 400);
        var pointer = new Point(95, 120); // outside raw rect, inside +hysteresis

        var shouldSwitch = CrossPageInputSwitchPolicies.ShouldSwitchByPointer(
            rect,
            pointer,
            hysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeFalse();
    }

    [Fact]
    public void ShouldSwitchByPointer_ShouldReturnTrue_WhenPointerOutsideExpandedRect()
    {
        var rect = new Rect(100, 100, 300, 400);
        var pointer = new Point(80, 120); // outside +hysteresis

        var shouldSwitch = CrossPageInputSwitchPolicies.ShouldSwitchByPointer(
            rect,
            pointer,
            hysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeTrue();
    }
}

public sealed class CrossPageInputSwitchRequestPolicyTests
{
    [Fact]
    public void ShouldSwitchForInput_ShouldReturnFalse_WhenGateRejects()
    {
        var shouldSwitch = CrossPageInputSwitchPolicies.ShouldSwitchForInput(
            photoModeActive: false,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: new Rect(100, 100, 200, 200),
            pointer: new Point(20, 20),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeFalse();
    }

    [Fact]
    public void ShouldSwitchForInput_ShouldReturnTrue_WhenNoCurrentRectButGateAndBitmapPass()
    {
        var shouldSwitch = CrossPageInputSwitchPolicies.ShouldSwitchForInput(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: null,
            pointer: new Point(20, 20),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeTrue();
    }

    [Fact]
    public void ShouldSwitchForInput_ShouldReturnFalse_WhenPointerInsideHysteresisRange()
    {
        var shouldSwitch = CrossPageInputSwitchPolicies.ShouldSwitchForInput(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Brush,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: new Rect(100, 100, 300, 300),
            pointer: new Point(95, 120),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeFalse();
    }

    [Fact]
    public void ShouldSwitchForInput_ShouldReturnTrue_WhenPointerOutsideHysteresisRange()
    {
        var shouldSwitch = CrossPageInputSwitchPolicies.ShouldSwitchForInput(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            boardActive: false,
            mode: PaintToolMode.Eraser,
            photoPanning: false,
            crossPageDragging: false,
            hasBitmap: true,
            currentPageRect: new Rect(100, 100, 300, 300),
            pointer: new Point(20, 20),
            pointerHysteresisDip: CrossPageInputSwitchThresholds.PointerHysteresisDip);

        shouldSwitch.Should().BeTrue();
    }
}

public sealed class CrossPageInputSwitchTargetPolicyTests
{
    [Theory]
    [InlineData(3, 3, 3)]
    [InlineData(3, 8, 4)]
    [InlineData(3, 1, 2)]
    [InlineData(1, -5, 0)]
    public void ResolveNeighborTargetPage_ShouldMatchExpected(
        int currentPage,
        int requestedPage,
        int expected)
    {
        var target = CrossPageInputSwitchPolicies.ResolveNeighborTargetPage(
            currentPage,
            requestedPage);

        target.Should().Be(expected);
    }
}

public sealed class CrossPageInteractiveSwitchClampPolicyTests
{
    [Fact]
    public void ClampTranslateY_ShouldClampToUpperBound_WhenCandidateExceedsRange()
    {
        double Height(int _) => 200;

        var clamped = CrossPageInputSwitchPolicies.ClampTranslateY(
            candidateTranslateY: 900,
            targetPage: 3,
            totalPages: 3,
            viewportHeight: 500,
            getPageHeight: Height,
            fallbackPageHeight: 200);

        clamped.Should().BeApproximately(400, 0.001);
    }

    [Fact]
    public void ClampTranslateY_ShouldClampToLowerBound_WhenCandidateBelowRange()
    {
        double Height(int _) => 300;

        var clamped = CrossPageInputSwitchPolicies.ClampTranslateY(
            candidateTranslateY: -900,
            targetPage: 2,
            totalPages: 4,
            viewportHeight: 400,
            getPageHeight: Height,
            fallbackPageHeight: 300);

        clamped.Should().BeApproximately(-500, 0.001);
    }

    [Fact]
    public void ClampTranslateY_ShouldUseFallbackHeight_WhenResolverReturnsZero()
    {
        double Height(int page) => page == 2 ? 260 : 0;

        var clamped = CrossPageInputSwitchPolicies.ClampTranslateY(
            candidateTranslateY: 700,
            targetPage: 2,
            totalPages: 3,
            viewportHeight: 400,
            getPageHeight: Height,
            fallbackPageHeight: 250);

        // above=250, target=260, below=250 => max=250, min=-110
        clamped.Should().BeApproximately(250, 0.001);
    }
}

public sealed class CrossPageInteractiveSwitchRefreshPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnDeferred_WhenRequested()
    {
        var mode = CrossPageInputSwitchPolicies.ResolveCrossPageInteractiveSwitchRefresh(
            PaintToolMode.Brush,
            deferCrossPageDisplayUpdate: true);

        mode.Should().Be(CrossPageInteractiveSwitchRefreshMode.DeferredByInput);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediateDirect_ForBrushWithoutDefer()
    {
        var mode = CrossPageInputSwitchPolicies.ResolveCrossPageInteractiveSwitchRefresh(
            PaintToolMode.Brush,
            deferCrossPageDisplayUpdate: false);

        mode.Should().Be(CrossPageInteractiveSwitchRefreshMode.ImmediateDirect);
    }

    [Fact]
    public void Resolve_ShouldReturnImmediateScheduled_ForNonBrushWithoutDefer()
    {
        var mode = CrossPageInputSwitchPolicies.ResolveCrossPageInteractiveSwitchRefresh(
            PaintToolMode.Eraser,
            deferCrossPageDisplayUpdate: false);

        mode.Should().Be(CrossPageInteractiveSwitchRefreshMode.ImmediateScheduled);
    }
}
