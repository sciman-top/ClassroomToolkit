using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System;
using System.Windows;
using Xunit;

namespace ClassroomToolkit.Tests.Paint;

public sealed class CrossPageInteractiveHoldDurationPolicyTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(2, 0)]
    [InlineData(3, 0)]
    [InlineData(5, 0)]
    public void ResolveMs_ShouldScaleWithVisibleNeighbors(int visibleNeighbors, int expectedMs)
    {
        var ms = CrossPageNeighborInkPolicies.ResolveMs(visibleNeighbors);
        ms.Should().Be(expectedMs);
    }

    [Fact]
    public void ResolveMs_ShouldClampToAtLeastOne()
    {
        var ms = CrossPageNeighborInkPolicies.ResolveMs(
            visibleNeighborPages: -5,
            mode: PaintToolMode.Brush,
            baseMs: -100,
            extraPerNeighborMs: -20,
            maxMs: 0);

        ms.Should().Be(1);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForBrushMode()
    {
        var cursorMs = CrossPageNeighborInkPolicies.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Cursor);
        var brushMs = CrossPageNeighborInkPolicies.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Brush);

        cursorMs.Should().Be(0);
        brushMs.Should().BeGreaterThan(cursorMs);
    }

    [Fact]
    public void ResolveMs_ShouldIncreaseForEraserMode()
    {
        var cursorMs = CrossPageNeighborInkPolicies.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Cursor);
        var eraserMs = CrossPageNeighborInkPolicies.ResolveMs(
            visibleNeighborPages: 2,
            mode: PaintToolMode.Eraser);

        cursorMs.Should().Be(0);
        eraserMs.Should().BeGreaterThan(cursorMs);
    }
}

public sealed class CrossPageInteractiveInkClearPolicyTests
{
    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnFalse_WhenInteractionIsActive()
    {
        var shouldClear = CrossPageNeighborInkPolicies.ShouldClearCurrentFrame(
            holdInkReplacement: false,
            hasNeighborInkStrokes: false,
            inkOperationActive: false,
            interactionActive: true);

        shouldClear.Should().BeFalse();
    }

    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnFalse_WhenInkOperationIsActive()
    {
        var shouldClear = CrossPageNeighborInkPolicies.ShouldClearCurrentFrame(
            holdInkReplacement: false,
            hasNeighborInkStrokes: false,
            inkOperationActive: true,
            interactionActive: false);

        shouldClear.Should().BeFalse();
    }

    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnFalse_WhenHoldReplacementIsTrue()
    {
        var shouldClear = CrossPageNeighborInkPolicies.ShouldClearCurrentFrame(
            holdInkReplacement: true,
            hasNeighborInkStrokes: false,
            inkOperationActive: false,
            interactionActive: false);

        shouldClear.Should().BeFalse();
    }

    [Fact]
    public void ShouldClearCurrentFrame_ShouldReturnTrue_OnlyWhenStableNoHoldNoInteractionAndNoNeighborStrokes()
    {
        var shouldClear = CrossPageNeighborInkPolicies.ShouldClearCurrentFrame(
            holdInkReplacement: false,
            hasNeighborInkStrokes: false,
            inkOperationActive: false,
            interactionActive: false);

        shouldClear.Should().BeTrue();
    }
}

public sealed class CrossPageInteractiveInkFrameHoldPolicyTests
{
    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenNoCurrentFrame()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageNeighborInkPolicies.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(100),
            nowUtc: now,
            hasCurrentInkFrame: false);

        hold.Should().BeFalse();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnTrue_WhenPinnedAndWithinWindow()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageNeighborInkPolicies.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(80),
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeTrue();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenExpired()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageNeighborInkPolicies.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(-1),
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeFalse();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenPageNotPinned()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageNeighborInkPolicies.ShouldHoldReplacement(
            pageIndex: 3,
            pinnedNeighborPage: 2,
            holdUntilUtc: now.AddMilliseconds(80),
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeFalse();
    }

    [Fact]
    public void ShouldHoldReplacement_ShouldReturnFalse_WhenHoldTimestampNotInitialized()
    {
        var now = DateTime.UtcNow;
        var hold = CrossPageNeighborInkPolicies.ShouldHoldReplacement(
            pageIndex: 2,
            pinnedNeighborPage: 2,
            holdUntilUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now,
            hasCurrentInkFrame: true);

        hold.Should().BeFalse();
    }
}

public sealed class CrossPageInteractiveInkReplacementPolicyTests
{
    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenResolvedInkMissing()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: false,
            holdInkReplacement: false,
            hasCurrentInkFrame: false,
            slotPageChanged: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenHoldActiveAndCurrentFrameExistsAndSlotChanged()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: true,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedAndCurrentFrameExists()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: true,
            holdInkReplacement: false,
            hasCurrentInkFrame: true,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedAndHoldActive()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: true,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenNoCurrentFrame()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: false,
            slotPageChanged: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenHoldActiveButSlotUnchanged()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: true,
            holdInkReplacement: true,
            hasCurrentInkFrame: true,
            slotPageChanged: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenHoldInactiveAndSlotUnchanged()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractiveInkReplacement(
            hasResolvedInkBitmap: true,
            holdInkReplacement: false,
            hasCurrentInkFrame: true,
            slotPageChanged: false).Should().BeTrue();
    }
}

public sealed class CrossPageInteractiveInkSlotRemapPolicyTests
{
    [Fact]
    public void Resolve_ShouldUsePreservedFrame_WhenSlotChangedDuringInkOperationAndPreservedExists()
    {
        var action = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveInkSlotRemap(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: true,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame);
    }

    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedDuringInkOperationAndNoPreservedFrame()
    {
        var action = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveInkSlotRemap(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: false,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedWithoutInkOperationAndNoPreservedFrame()
    {
        var action = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveInkSlotRemap(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: false,
            inkOperationActive: false);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.ClearCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldUsePreservedFrame_WhenSlotChangedWithoutInkOperationAndPreservedExists()
    {
        var action = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveInkSlotRemap(
            slotPageChanged: true,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: true,
            inkOperationActive: false);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.UsePreservedFrame);
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenSlotNotChanged()
    {
        var action = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveInkSlotRemap(
            slotPageChanged: false,
            hasResolvedInkBitmap: false,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: true,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame);
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenResolvedInkBitmapExists()
    {
        var action = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveInkSlotRemap(
            slotPageChanged: true,
            hasResolvedInkBitmap: true,
            hasCurrentInkFrame: true,
            hasPreservedInkFrame: false,
            inkOperationActive: true);

        action.Should().Be(CrossPageInteractiveInkSlotRemapAction.KeepCurrentFrame);
    }
}

public sealed class CrossPageInteractiveNeighborInkHoldPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnTrue_WhenBaseHoldIsTrue()
    {
        var hold = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveNeighborInkHold(
            baseHoldReplacement: true,
            interactionActive: false,
            hasCurrentInkFrame: false,
            inkOperationActive: false,
            slotPageChanged: false);

        hold.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnTrue_WhenInteractionActiveAndInkOperationActiveAndCurrentFrameExists()
    {
        var hold = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveNeighborInkHold(
            baseHoldReplacement: false,
            interactionActive: true,
            hasCurrentInkFrame: true,
            inkOperationActive: true,
            slotPageChanged: false);

        hold.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldReturnFalse_WhenInteractionActiveButNoInkOperation()
    {
        var hold = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveNeighborInkHold(
            baseHoldReplacement: false,
            interactionActive: true,
            hasCurrentInkFrame: true,
            inkOperationActive: false,
            slotPageChanged: false);

        hold.Should().BeFalse();
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void Resolve_ShouldReturnFalse_WhenNoBaseHoldAndNoCurrentFrame(
        bool interactionActive,
        bool hasCurrentInkFrame)
    {
        var hold = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveNeighborInkHold(
            baseHoldReplacement: false,
            interactionActive: interactionActive,
            hasCurrentInkFrame: hasCurrentInkFrame,
            inkOperationActive: false,
            slotPageChanged: false);

        hold.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, true, true)]
    public void Resolve_ShouldReturnFalse_WhenSlotPageChanged(
        bool baseHoldReplacement,
        bool interactionActive,
        bool hasCurrentInkFrame,
        bool inkOperationActive)
    {
        var hold = CrossPageNeighborInkPolicies.ResolveCrossPageInteractiveNeighborInkHold(
            baseHoldReplacement: baseHoldReplacement,
            interactionActive: interactionActive,
            hasCurrentInkFrame: hasCurrentInkFrame,
            inkOperationActive: inkOperationActive,
            slotPageChanged: true);

        hold.Should().BeFalse();
    }
}

public sealed class CrossPageInteractivePageReplacementPolicyTests
{
    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenTargetMissing()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractivePageReplacement(
            hasResolvedTargetFrame: false,
            interactionActive: false,
            slotPageChanged: true,
            hasCurrentFrame: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_ForStableSlotDuringInteraction()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractivePageReplacement(
            hasResolvedTargetFrame: true,
            interactionActive: true,
            slotPageChanged: false,
            hasCurrentFrame: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedDuringInteraction()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractivePageReplacement(
            hasResolvedTargetFrame: true,
            interactionActive: true,
            slotPageChanged: true,
            hasCurrentFrame: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenNoCurrentFrame()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceCrossPageInteractivePageReplacement(
            hasResolvedTargetFrame: true,
            interactionActive: true,
            slotPageChanged: false,
            hasCurrentFrame: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldReuseCurrentFrame_ShouldReturnFalse_WhenSlotChanged()
    {
        CrossPageNeighborInkPolicies.ShouldReuseCurrentFrame(
            shouldReplacePageFrame: false,
            slotPageChanged: true,
            hasCurrentFrame: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReuseCurrentFrame_ShouldReturnTrue_WhenSlotStableAndTargetNotReplaced()
    {
        CrossPageNeighborInkPolicies.ShouldReuseCurrentFrame(
            shouldReplacePageFrame: false,
            slotPageChanged: false,
            hasCurrentFrame: true).Should().BeTrue();
    }
}

public sealed class CrossPageInteractivePinLifetimePolicyTests
{
    [Fact]
    public void ShouldReleasePin_ShouldReturnFalse_WhenHoldNotArmed()
    {
        var now = DateTime.UtcNow;
        CrossPageNeighborInkPolicies.ShouldReleasePin(
            holdUntilUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now,
            interactionActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldReleasePin_ShouldReturnFalse_WhenInteractionActive()
    {
        var now = DateTime.UtcNow;
        CrossPageNeighborInkPolicies.ShouldReleasePin(
            holdUntilUtc: now.AddMilliseconds(-50),
            nowUtc: now,
            interactionActive: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldReleasePin_ShouldReturnTrue_WhenExpiredAndNoInteraction()
    {
        var now = DateTime.UtcNow;
        CrossPageNeighborInkPolicies.ShouldReleasePin(
            holdUntilUtc: now.AddMilliseconds(-1),
            nowUtc: now,
            interactionActive: false).Should().BeTrue();
    }
}

public sealed class CrossPageInteractiveSeedInkFramePolicyTests
{
    [Fact]
    public void ShouldReplaceFrame_ShouldReturnTrue_WhenInkDisplayDisabled()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceFrame(
                inkShowEnabled: false,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: false,
                slotPageChanged: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldReturnTrue_WhenTargetFrameResolved()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: true,
                slotPageChanged: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldKeepCurrentFrame_WhenTargetMissingAndCurrentExists()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: false,
                slotPageChanged: false)
            .Should()
            .BeFalse();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldAllowNullAssignment_WhenNoCurrentFrame()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: false,
                hasResolvedTargetFrame: false,
                slotPageChanged: false)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldReplaceFrame_ShouldClearStaleFrame_WhenSlotRemappedAndTargetMissing()
    {
        CrossPageNeighborInkPolicies.ShouldReplaceFrame(
                inkShowEnabled: true,
                hasCurrentFrame: true,
                hasResolvedTargetFrame: false,
                slotPageChanged: true)
            .Should()
            .BeTrue();
    }
}

public sealed class CrossPageMutationNeighborInkCarryoverPolicyTests
{
    [Fact]
    public void ShouldClearPreservedNeighborInkFrames_ShouldReturnTrue_ForBrushMutationSwitch()
    {
        var shouldClear = CrossPageNeighborInkPolicies.ShouldClearPreservedNeighborInkFrames(
            pageChanged: true,
            interactiveSwitch: false,
            inputTriggeredByActiveInkMutation: true,
            mode: PaintToolMode.Brush);

        shouldClear.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false, true, PaintToolMode.Brush)]
    [InlineData(true, true, true, PaintToolMode.Brush)]
    [InlineData(true, false, false, PaintToolMode.Brush)]
    [InlineData(true, false, true, PaintToolMode.Eraser)]
    [InlineData(true, false, true, PaintToolMode.RegionErase)]
    public void ShouldClearPreservedNeighborInkFrames_ShouldReturnFalse_WhenGuardNotMet(
        bool pageChanged,
        bool interactiveSwitch,
        bool inputTriggeredByActiveInkMutation,
        PaintToolMode mode)
    {
        var shouldClear = CrossPageNeighborInkPolicies.ShouldClearPreservedNeighborInkFrames(
            pageChanged,
            interactiveSwitch,
            inputTriggeredByActiveInkMutation,
            mode);

        shouldClear.Should().BeFalse();
    }
}

public sealed class CrossPageMutationNeighborRetentionPolicyTests
{
    [Fact]
    public void ResolvePreservedPage_ShouldReturnPreviousPage_WhenClearAndPageChanged()
    {
        var preserved = CrossPageNeighborInkPolicies.ResolvePreservedPage(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 5,
            currentPage: 6);

        preserved.Should().Be(5);
    }

    [Fact]
    public void ResolvePreservedPage_ShouldReturnZero_WhenPreviousPageInvalid()
    {
        var preserved = CrossPageNeighborInkPolicies.ResolvePreservedPage(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 0,
            currentPage: 6);

        preserved.Should().Be(0);
    }

    [Fact]
    public void ResolvePreservedPage_ShouldReturnZero_WhenNotMutationClear()
    {
        var preserved = CrossPageNeighborInkPolicies.ResolvePreservedPage(
            clearPreservedNeighborInkFrames: false,
            pageChanged: true,
            previousPage: 5,
            currentPage: 6);

        preserved.Should().Be(0);
    }
}

public sealed class CrossPageMutationNeighborSeedPolicyTests
{
    [Fact]
    public void ShouldSeedPreviousPageAfterClear_ShouldReturnTrue_WhenPageChangedAndClearWasRequested()
    {
        var shouldSeed = CrossPageNeighborInkPolicies.ShouldSeedPreviousPageAfterClear(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 3,
            currentPage: 4);

        shouldSeed.Should().BeTrue();
    }

    [Fact]
    public void ShouldSeedPreviousPageAfterClear_ShouldReturnFalse_WhenNoClearWasRequested()
    {
        var shouldSeed = CrossPageNeighborInkPolicies.ShouldSeedPreviousPageAfterClear(
            clearPreservedNeighborInkFrames: false,
            pageChanged: true,
            previousPage: 3,
            currentPage: 4);

        shouldSeed.Should().BeFalse();
    }

    [Fact]
    public void ShouldSeedPreviousPageAfterClear_ShouldReturnFalse_WhenPreviousPageIsInvalid()
    {
        var shouldSeed = CrossPageNeighborInkPolicies.ShouldSeedPreviousPageAfterClear(
            clearPreservedNeighborInkFrames: true,
            pageChanged: true,
            previousPage: 0,
            currentPage: 4);

        shouldSeed.Should().BeFalse();
    }
}

public sealed class CrossPageNeighborBitmapResolvePolicyTests
{
    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnFalse_WhenInteractionActiveAndSlotChanged()
    {
        var allowed = CrossPageNeighborInkPolicies.ShouldAllowSynchronousResolveCrossPageNeighborBitmapResolve(
            interactionActive: true,
            slotPageChanged: true);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_WhenInteractionInactive()
    {
        var allowed = CrossPageNeighborInkPolicies.ShouldAllowSynchronousResolveCrossPageNeighborBitmapResolve(
            interactionActive: false,
            slotPageChanged: true);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_WhenSlotNotChanged()
    {
        var allowed = CrossPageNeighborInkPolicies.ShouldAllowSynchronousResolveCrossPageNeighborBitmapResolve(
            interactionActive: true,
            slotPageChanged: false);

        allowed.Should().BeTrue();
    }
}

public sealed class CrossPageNeighborHeightResolvePolicyTests
{
    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnFalse_ForImageDuringInteraction()
    {
        var allowed = CrossPageNeighborInkPolicies.ShouldAllowSynchronousResolveCrossPageNeighborHeightResolve(
            interactionActive: true,
            photoDocumentIsPdf: false);

        allowed.Should().BeFalse();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_ForImageWhenNotInteracting()
    {
        var allowed = CrossPageNeighborInkPolicies.ShouldAllowSynchronousResolveCrossPageNeighborHeightResolve(
            interactionActive: false,
            photoDocumentIsPdf: false);

        allowed.Should().BeTrue();
    }

    [Fact]
    public void ShouldAllowSynchronousResolve_ShouldReturnTrue_ForPdfDuringInteraction()
    {
        var allowed = CrossPageNeighborInkPolicies.ShouldAllowSynchronousResolveCrossPageNeighborHeightResolve(
            interactionActive: true,
            photoDocumentIsPdf: true);

        allowed.Should().BeTrue();
    }
}

public sealed class CrossPageNeighborInkFramePolicyTests
{
    [Fact]
    public void Resolve_ShouldClearCurrentFrame_WhenSlotChangedWithoutResolvedInk()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: false,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenHeld()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: true,
            holdInkReplacement: true,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: true);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldAllowReplacement_WhenResolvedBitmapCanReplace()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            hasTargetInkStrokes: true,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: true);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeTrue();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepVisible_WhenUsingPreservedFrame()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            hasTargetInkStrokes: true,
            holdInkReplacement: false,
            usedPreservedInkFrame: true,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldClear_WhenNoFrameAndNoPreserveAndNoHold()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            hasTargetInkStrokes: false,
            holdInkReplacement: false,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeTrue();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenTargetInkIsUnresolvedAndSlotDidNotChange()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: false,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: false,
            holdInkReplacement: true,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldKeepCurrentFrame_WhenTargetInkIsUnresolvedButHoldRequested()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveFrame(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            hasTargetInkStrokes: false,
            holdInkReplacement: true,
            usedPreservedInkFrame: false,
            hasResolvedInkBitmap: false);

        decision.ClearCurrentFrame.Should().BeFalse();
        decision.AllowResolvedInkReplacement.Should().BeFalse();
        decision.KeepVisible.Should().BeTrue();
    }

    [Fact]
    public void ShouldClearWhenUnresolved_ShouldReturnTrue_WhenDecisionRequiresClearAndInkIsUnresolved()
    {
        var decision = new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: true,
            AllowResolvedInkReplacement: false,
            KeepVisible: false);

        CrossPageNeighborInkPolicies.ShouldClearWhenUnresolved(
            decision,
            hasResolvedInkBitmap: false).Should().BeTrue();
    }

    [Fact]
    public void ShouldClearWhenUnresolved_ShouldReturnFalse_WhenResolvedInkBitmapExists()
    {
        var decision = new CrossPageNeighborInkFrameDecision(
            ClearCurrentFrame: true,
            AllowResolvedInkReplacement: true,
            KeepVisible: true);

        CrossPageNeighborInkPolicies.ShouldClearWhenUnresolved(
            decision,
            hasResolvedInkBitmap: true).Should().BeFalse();
    }
}

public sealed class CrossPageNeighborInkPolicyTests
{
    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldReturnTrue_ForSameSlotWithExistingFrame()
    {
        CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
                slotPageChanged: false,
                hasExistingInkFrame: true)
            .Should()
            .BeTrue();
    }

    [Fact]
    public void ShouldKeepExistingInkFrame_ShouldReturnFalse_WhenSlotChanged()
    {
        CrossPageNeighborInkPolicies.ShouldKeepExistingInkFrame(
                slotPageChanged: true,
                hasExistingInkFrame: true)
            .Should()
            .BeFalse();
    }
}

public sealed class CrossPageNeighborInkRenderAdmissionPolicyTests
{
    [Fact]
    public void ShouldRejectStaleCacheKey_ShouldReturnTrue_WhenExpectedKeyIsEmpty()
    {
        CrossPageNeighborInkPolicies.ShouldRejectStaleCacheKey(
            cacheKey: "doc|1",
            expectedCacheKey: string.Empty).Should().BeTrue();
    }

    [Fact]
    public void ShouldRejectStaleCacheKey_ShouldReturnTrue_WhenCacheKeyDoesNotMatchExpected()
    {
        CrossPageNeighborInkPolicies.ShouldRejectStaleCacheKey(
            cacheKey: "docA|1",
            expectedCacheKey: "docB|1").Should().BeTrue();
    }

    [Fact]
    public void ShouldRejectStaleCacheKey_ShouldReturnFalse_WhenCacheKeyMatchesExpectedExactly()
    {
        CrossPageNeighborInkPolicies.ShouldRejectStaleCacheKey(
            cacheKey: "doc|1",
            expectedCacheKey: "doc|1").Should().BeFalse();
    }
}

public sealed class CrossPageNeighborInkRenderSurfacePolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepOriginalSurface_WhenStrokeBoundsStayInsidePageWidth()
    {
        var plan = CrossPageNeighborInkPolicies.ResolveRenderSurface(
            pagePixelWidth: 1200,
            pagePixelHeight: 1800,
            dpiX: 96,
            pageWidthDip: 1200,
            minStrokeXDip: 10,
            maxStrokeXDip: 1180);

        plan.PixelWidth.Should().Be(1200);
        plan.PixelHeight.Should().Be(1800);
        plan.HorizontalOffsetDip.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldExpandSurfaceAndOffset_WhenStrokeOverflowsBothSides()
    {
        var plan = CrossPageNeighborInkPolicies.ResolveRenderSurface(
            pagePixelWidth: 1200,
            pagePixelHeight: 1800,
            dpiX: 96,
            pageWidthDip: 1200,
            minStrokeXDip: -32,
            maxStrokeXDip: 1248);

        plan.PixelWidth.Should().Be(1280);
        plan.PixelHeight.Should().Be(1800);
        plan.HorizontalOffsetDip.Should().Be(32);
    }

    [Fact]
    public void Resolve_ShouldClampOverflowToBudget_WhenStrokeExtendsTooFarOutsidePage()
    {
        var plan = CrossPageNeighborInkPolicies.ResolveRenderSurface(
            pagePixelWidth: 1000,
            pagePixelHeight: 1600,
            dpiX: 96,
            pageWidthDip: 1000,
            minStrokeXDip: -2000,
            maxStrokeXDip: 3200);

        plan.PixelWidth.Should().Be(1000 + (CrossPageNeighborInkPolicies.MaxHorizontalOverflowDipPerSidePx * 2));
        plan.HorizontalOffsetDip.Should().Be(CrossPageNeighborInkPolicies.MaxHorizontalOverflowDipPerSidePx);
    }
}

public sealed class CrossPageNeighborInkReplacementPolicyTests
{
    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenNoCurrentInkFrame()
    {
        var shouldReplace = CrossPageNeighborInkPolicies.ShouldReplaceReplacement(
            slotPageChanged: true,
            hasCurrentInkFrame: false,
            usedPreservedInkFrame: false);

        shouldReplace.Should().BeTrue();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenSlotUnchangedAndCurrentFrameExists()
    {
        var shouldReplace = CrossPageNeighborInkPolicies.ShouldReplaceReplacement(
            slotPageChanged: false,
            hasCurrentInkFrame: true,
            usedPreservedInkFrame: false);

        shouldReplace.Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnFalse_WhenSlotChangedButUsingPreservedFrame()
    {
        var shouldReplace = CrossPageNeighborInkPolicies.ShouldReplaceReplacement(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            usedPreservedInkFrame: true);

        shouldReplace.Should().BeFalse();
    }

    [Fact]
    public void ShouldReplace_ShouldReturnTrue_WhenSlotChangedWithoutPreservedFrame()
    {
        var shouldReplace = CrossPageNeighborInkPolicies.ShouldReplaceReplacement(
            slotPageChanged: true,
            hasCurrentInkFrame: true,
            usedPreservedInkFrame: false);

        shouldReplace.Should().BeTrue();
    }
}

public sealed class CrossPageNeighborPageCandidatePolicyTests
{
    [Theory]
    [InlineData(Visibility.Collapsed, true, true, 4, 3, true, true, false)]
    [InlineData(Visibility.Visible, false, true, 4, 3, true, true, false)]
    [InlineData(Visibility.Visible, true, false, 4, 3, true, true, false)]
    [InlineData(Visibility.Visible, true, true, 6, 3, true, true, false)]
    [InlineData(Visibility.Visible, true, true, 4, 3, false, true, false)]
    [InlineData(Visibility.Visible, true, true, 4, 3, true, false, false)]
    [InlineData(Visibility.Visible, true, true, 4, 3, true, true, true)]
    [InlineData(Visibility.Visible, true, true, 2, 3, true, true, true)]
    public void ShouldUseCandidate_ShouldMatchExpected(
        Visibility visibility,
        bool hasBitmap,
        bool hasCandidatePage,
        int candidatePage,
        int currentPage,
        bool hasRect,
        bool pointerInsideRect,
        bool expected)
    {
        var result = CrossPageNeighborInkPolicies.ShouldUseCandidate(
            visibility,
            hasBitmap,
            hasCandidatePage,
            candidatePage,
            currentPage,
            hasRect,
            pointerInsideRect);

        result.Should().Be(expected);
    }
}

public sealed class CrossPageNeighborPageDedupPolicyTests
{
    [Fact]
    public void Resolve_ShouldReturnSameList_WhenNoDuplicates()
    {
        var input = new List<(int PageIndex, double Top)>
        {
            (2, -100),
            (3, 200)
        };

        var result = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageDedup(input);

        result.Should().Equal(input);
    }

    [Fact]
    public void Resolve_ShouldKeepFirstOccurrence_WhenDuplicatesExist()
    {
        var result = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageDedup(
            new List<(int PageIndex, double Top)>
            {
                (2, -120),
                (3, 180),
                (2, -110),
                (4, 520),
                (3, 200)
            });

        result.Should().Equal(
            new List<(int PageIndex, double Top)>
            {
                (2, -120),
                (3, 180),
                (4, 520)
            });
    }
}


public sealed class CrossPageNeighborPageFramePolicyTests
{
    [Fact]
    public void Resolve_ShouldNotHoldOrCollapse_WhenTargetFrameResolved()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageFrame(
            slotPageChanged: true,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: true,
            interactionActive: true);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCollapse_WhenSlotChangedAndTargetFrameMissing()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageFrame(
            slotPageChanged: true,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: false,
            interactionActive: false);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldCollapse_WhenInteractionActiveButSlotChangedAndNoContinuityPreference()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageFrame(
            slotPageChanged: true,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: false,
            interactionActive: true,
            preferHoldCurrentFrameOnSlotRemap: false);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldHoldCurrent_WhenInteractionActiveAndCurrentFrameExists()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageFrame(
            slotPageChanged: false,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: false,
            interactionActive: true);

        decision.HoldCurrentFrame.Should().BeTrue();
        decision.CollapseSlot.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldCollapse_WhenNoTargetFrameAndNoCurrentFrame()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageFrame(
            slotPageChanged: true,
            hasCurrentFrame: false,
            hasResolvedTargetFrame: false,
            interactionActive: true);

        decision.HoldCurrentFrame.Should().BeFalse();
        decision.CollapseSlot.Should().BeTrue();
    }

    [Fact]
    public void Resolve_ShouldHoldCurrent_WhenZoomPrefersContinuityOnSlotRemap()
    {
        var decision = CrossPageNeighborInkPolicies.ResolveCrossPageNeighborPageFrame(
            slotPageChanged: true,
            hasCurrentFrame: true,
            hasResolvedTargetFrame: false,
            interactionActive: true,
            preferHoldCurrentFrameOnSlotRemap: true);

        decision.HoldCurrentFrame.Should().BeTrue();
        decision.CollapseSlot.Should().BeFalse();
    }
}

public sealed class CrossPageNeighborPagesClearPolicyTests
{
    [Fact]
    public void ShouldKeepFrames_ShouldReturnFalse_WhenNoVisibleFrame()
    {
        var result = CrossPageNeighborInkPolicies.ShouldKeepFrames(
            hasVisibleNeighborFrame: false,
            interactionActive: true,
            lastNonEmptyUtc: DateTime.UtcNow,
            nowUtc: DateTime.UtcNow,
            clearGraceMs: 180);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldKeepFrames_ShouldReturnTrue_WhenInteractionActive()
    {
        var now = DateTime.UtcNow;
        var result = CrossPageNeighborInkPolicies.ShouldKeepFrames(
            hasVisibleNeighborFrame: true,
            interactionActive: true,
            lastNonEmptyUtc: now.AddMilliseconds(-1000),
            nowUtc: now,
            clearGraceMs: 180);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldKeepFrames_ShouldRespectGraceWindow_WhenIdle()
    {
        var now = DateTime.UtcNow;
        var keep = CrossPageNeighborInkPolicies.ShouldKeepFrames(
            hasVisibleNeighborFrame: true,
            interactionActive: false,
            lastNonEmptyUtc: now.AddMilliseconds(-80),
            nowUtc: now,
            clearGraceMs: 180);
        var clear = CrossPageNeighborInkPolicies.ShouldKeepFrames(
            hasVisibleNeighborFrame: true,
            interactionActive: false,
            lastNonEmptyUtc: now.AddMilliseconds(-400),
            nowUtc: now,
            clearGraceMs: 180);

        keep.Should().BeTrue();
        clear.Should().BeFalse();
    }
}

public sealed class CrossPageNeighborPrefetchGatePolicyTests
{
    [Fact]
    public void ShouldSchedule_ShouldReturnFalse_WhenNotPhotoModeOrPdf()
    {
        CrossPageNeighborInkPolicies.ShouldSchedule(
            photoModeActive: false,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: true,
            interactionActive: false).Should().BeFalse();

        CrossPageNeighborInkPolicies.ShouldSchedule(
            photoModeActive: true,
            photoDocumentIsPdf: true,
            crossPageDisplayEnabled: true,
            interactionActive: false).Should().BeFalse();
    }

    [Fact]
    public void ShouldSchedule_ShouldBlock_WhenCrossPageDisabledAndInteractionActive()
    {
        var result = CrossPageNeighborInkPolicies.ShouldSchedule(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: false,
            interactionActive: true);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldRunPrefetch_ShouldRequireIdleImagePhotoMode()
    {
        CrossPageNeighborInkPolicies.ShouldRunPrefetch(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: true,
            interactionActive: false).Should().BeTrue();

        CrossPageNeighborInkPolicies.ShouldRunPrefetch(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: true,
            interactionActive: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldRunPrefetch_ShouldReturnFalse_WhenCrossPageDisplayDisabled()
    {
        var result = CrossPageNeighborInkPolicies.ShouldRunPrefetch(
            photoModeActive: true,
            photoDocumentIsPdf: false,
            crossPageDisplayEnabled: false,
            interactionActive: false);

        result.Should().BeFalse();
    }
}
