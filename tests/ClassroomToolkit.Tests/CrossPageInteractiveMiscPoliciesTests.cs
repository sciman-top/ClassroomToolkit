using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class BoardTransitionCrossPagePolicyTests
{
    [Fact]
    public void ShouldHandleCrossPageArtifacts_ShouldReturnTrue_WhenPhotoModeAndCrossPageEnabled()
    {
        CrossPageInteractiveMiscPolicies.ShouldHandleCrossPageArtifacts(
            photoModeActive: true,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldHandleCrossPageArtifacts_ShouldReturnFalse_WhenPhotoModeDisabled()
    {
        CrossPageInteractiveMiscPolicies.ShouldHandleCrossPageArtifacts(
            photoModeActive: false,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void ShouldHandleCrossPageArtifacts_ShouldReturnFalse_WhenCrossPageDisabled()
    {
        CrossPageInteractiveMiscPolicies.ShouldHandleCrossPageArtifacts(
            photoModeActive: true,
            crossPageDisplayEnabled: false).Should().BeFalse();
    }
}

public sealed class CrossPageFrameSourceAssignmentPolicyTests
{
    [Fact]
    public void ShouldAssign_ShouldReturnFalse_WhenSameReferenceAndNotForced()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);

        var result = CrossPageInteractiveMiscPolicies.ShouldAssign(bitmap, bitmap);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAssign_ShouldReturnTrue_WhenClearingExistingSource()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);

        var result = CrossPageInteractiveMiscPolicies.ShouldAssign(bitmap, null);

        result.Should().BeTrue();
    }

    [Fact]
    public void ShouldAssign_ShouldReturnFalse_WhenBothSourcesNull()
    {
        var result = CrossPageInteractiveMiscPolicies.ShouldAssign(null, null);

        result.Should().BeFalse();
    }

    [Fact]
    public void ShouldAssign_ShouldReturnTrue_WhenForced()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 255, 0, 0, 255 },
            4);

        var result = CrossPageInteractiveMiscPolicies.ShouldAssign(bitmap, bitmap, forceAssign: true);

        result.Should().BeTrue();
    }
}

public sealed class CrossPageOutOfPageMoveSuppressionPolicyTests
{
    [Fact]
    public void ShouldSuppress_ShouldReturnTrue_WhenBrushStrokeIsOutsideCurrentPageInCrossPageMode()
    {
        var suppress = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeTrue();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenPointerStillInsideCurrentPage()
    {
        var suppress = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: true);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenNotBrushOrNotStrokeInProgress()
    {
        var suppressByTool = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            mode: PaintToolMode.Eraser,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);
        var suppressByState = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: false,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppressByTool.Should().BeFalse();
        suppressByState.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenSwitchHappenedThisFrame()
    {
        var suppress = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: true,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenInRecentSwitchGraceWindow()
    {
        var suppress = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: false,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: true,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeFalse();
    }

    [Fact]
    public void ShouldSuppress_ShouldReturnFalse_WhenPhotoFullscreenIsActive()
    {
        var suppress = CrossPageInteractiveMiscPolicies.ShouldSuppress(
            crossPageDisplayActive: true,
            photoFullscreenActive: true,
            mode: PaintToolMode.Brush,
            strokeInProgress: true,
            switchedPageThisFrame: false,
            recentSwitchGraceActive: false,
            hasCurrentPageRect: true,
            pointerInsideCurrentPageRect: false);

        suppress.Should().BeFalse();
    }
}

public sealed class CrossPagePendingTakeoverPolicyTests
{
    [Fact]
    public void Resolve_ShouldKeepSkipPending_WhenNotImmediate()
    {
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 1,
            PendingSinceUtc: DateTime.UtcNow.AddMilliseconds(-500));

        var result = CrossPageInteractiveMiscPolicies.ResolveCrossPagePendingTakeover(
            decision,
            CrossPageUpdateDispatchSuffix.None,
            state,
            DateTime.UtcNow);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.SkipPending);
    }

    [Fact]
    public void Resolve_ShouldUpgradeToDirect_WhenImmediateAndPendingTimedOut()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 1,
            PendingSinceUtc: nowUtc.AddMilliseconds(-200));

        var result = CrossPageInteractiveMiscPolicies.ResolveCrossPagePendingTakeover(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate,
            state,
            nowUtc,
            thresholdMs: 120);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.Direct);
        result.DelayMs.Should().Be(0);
    }

    [Fact]
    public void Resolve_ShouldKeepSkipPending_WhenImmediateButPendingWithinThreshold()
    {
        var nowUtc = DateTime.UtcNow;
        var decision = new CrossPageDisplayUpdateDispatchDecision(
            Mode: CrossPageDisplayUpdateDispatchMode.SkipPending,
            DelayMs: 0);
        var state = new CrossPageDisplayUpdateRuntimeState(
            Pending: true,
            Token: 1,
            PendingSinceUtc: nowUtc.AddMilliseconds(-40));

        var result = CrossPageInteractiveMiscPolicies.ResolveCrossPagePendingTakeover(
            decision,
            CrossPageUpdateDispatchSuffix.Immediate,
            state,
            nowUtc,
            thresholdMs: 120);

        result.Mode.Should().Be(CrossPageDisplayUpdateDispatchMode.SkipPending);
    }
}

public sealed class CrossPageRegionEraseNavigationPolicyTests
{
    [Fact]
    public void Resolve_ShouldUseStableNavigationPath()
    {
        var plan = CrossPageInteractiveMiscPolicies.ResolveCrossPageRegionEraseNavigation();

        plan.InteractiveSwitch.Should().BeFalse();
        plan.DeferCrossPageDisplayUpdate.Should().BeFalse();
    }
}

public sealed class CrossPageRegionEraseOrderPolicyTests
{
    [Fact]
    public void ResolveBatchOrder_ShouldMoveCurrentPageToTail()
    {
        var result = CrossPageInteractiveMiscPolicies.ResolveBatchOrder(
            [2, 3, 1],
            currentPage: 2);

        result.Should().Equal(1, 3, 2);
    }

    [Fact]
    public void ResolveBatchOrder_ShouldDistinctAndIgnoreInvalidPages()
    {
        var result = CrossPageInteractiveMiscPolicies.ResolveBatchOrder(
            [0, -1, 3, 3, 2],
            currentPage: 0);

        result.Should().Equal(2, 3);
    }

    [Fact]
    public void ResolveBatchOrder_ShouldFallbackToCurrent_WhenPagesNull()
    {
        var result = CrossPageInteractiveMiscPolicies.ResolveBatchOrder(
            null!,
            currentPage: 5);

        result.Should().Equal(5);
    }
}

public sealed class CrossPageRegionErasePolicyTests
{
    [Fact]
    public void ShouldUseCrossPageErase_ShouldReturnTrue_WhenPhotoInkAndCrossPageEnabled()
    {
        CrossPageInteractiveMiscPolicies.ShouldUseCrossPageErase(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true).Should().BeTrue();
    }

    [Fact]
    public void ShouldUseCrossPageErase_ShouldReturnFalse_WhenPhotoInkDisabled()
    {
        CrossPageInteractiveMiscPolicies.ShouldUseCrossPageErase(
            photoInkModeActive: false,
            crossPageDisplayEnabled: true).Should().BeFalse();
    }

    [Fact]
    public void CanNavigateForRegionErase_ShouldReturnFalse_WhenTargetPageInvalid()
    {
        CrossPageInteractiveMiscPolicies.CanNavigateForRegionErase(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true,
            targetPage: 0).Should().BeFalse();
    }

    [Fact]
    public void CanNavigateForRegionErase_ShouldReturnTrue_WhenAllConditionsMet()
    {
        CrossPageInteractiveMiscPolicies.CanNavigateForRegionErase(
            photoInkModeActive: true,
            crossPageDisplayEnabled: true,
            targetPage: 3).Should().BeTrue();
    }
}

public sealed class CrossPageViewportBoundsPolicyTests
{
    [Fact]
    public void ResolveSlackDip_ShouldRespectMinimumAndRatio()
    {
        CrossPageInteractiveMiscPolicies.ResolveSlackDip(40).Should().Be(32.0);
        CrossPageInteractiveMiscPolicies.ResolveSlackDip(200).Should().Be(100.0);
    }

    [Fact]
    public void IsTranslateClamped_ShouldUseConfiguredEpsilon()
    {
        CrossPageInteractiveMiscPolicies.IsTranslateClamped(100.0, 100.4).Should().BeFalse();
        CrossPageInteractiveMiscPolicies.IsTranslateClamped(100.0, 100.6).Should().BeTrue();
    }
}

public sealed class CrossPageZoomLayoutScalePolicyTests
{
    [Theory]
    [InlineData(1.25)]
    [InlineData(0.8)]
    public void ShouldSynchronize_ShouldReturnTrue_ForMeaningfulScaleFactors(double scaleFactor)
    {
        CrossPageInteractiveMiscPolicies.ShouldSynchronize(scaleFactor).Should().BeTrue();
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.0005)]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(double.NaN)]
    public void ShouldSynchronize_ShouldReturnFalse_ForInvalidOrTinyScaleFactors(double scaleFactor)
    {
        CrossPageInteractiveMiscPolicies.ShouldSynchronize(scaleFactor).Should().BeFalse();
    }

    [Fact]
    public void Scale_ShouldMultiplyPositiveHeightsAndNegativeOffsets()
    {
        CrossPageInteractiveMiscPolicies.Scale(240.0, 0.5).Should().Be(120.0);
        CrossPageInteractiveMiscPolicies.Scale(-180.0, 0.5).Should().Be(-90.0);
    }
}
