using AwesomeAssertions;
using ClassroomToolkit.App.Paint;
using System;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageDeferredRefreshGatePolicyTests
{
    [Fact]
    public void ResolveBeforeSchedule_ShouldBlock_WhenInactiveOrInteractionActive()
    {
        var inactive = CrossPageRefreshCoordinationPolicies.ResolveBeforeSchedule(
            crossPageDisplayActive: false,
            interactionActive: false);
        var interactionActive = CrossPageRefreshCoordinationPolicies.ResolveBeforeSchedule(
            crossPageDisplayActive: true,
            interactionActive: true);

        inactive.ShouldProceed.Should().BeFalse();
        inactive.Reason.Should().Be(CrossPageDeferredDiagnosticReason.Inactive);
        interactionActive.ShouldProceed.Should().BeFalse();
        interactionActive.Reason.Should().Be(CrossPageDeferredDiagnosticReason.InteractionActive);
    }

    [Fact]
    public void ResolveBeforeSchedule_ShouldProceed_WhenActiveAndInteractionIdle()
    {
        var decision = CrossPageRefreshCoordinationPolicies.ResolveBeforeSchedule(
            crossPageDisplayActive: true,
            interactionActive: false);

        decision.ShouldProceed.Should().BeTrue();
        decision.Reason.Should().BeNull();
    }

    [Fact]
    public void ResolveBeforeDelayedDispatch_ShouldUseCombinedReason_WhenBlocked()
    {
        var decision = CrossPageRefreshCoordinationPolicies.ResolveBeforeDelayedDispatch(
            crossPageDisplayActive: false,
            interactionActive: true);

        decision.ShouldProceed.Should().BeFalse();
        decision.Reason.Should().Be(CrossPageDeferredDiagnosticReason.InactiveOrInteractionActive);
    }
}

public sealed class CrossPageDeferredRefreshPolicyTests
{
    [Fact]
    public void ShouldArmOnInteractiveSwitch_ShouldReturnTrue_OnlyForDeferredMode()
    {
        CrossPageRefreshCoordinationPolicies.ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode.DeferredByInput)
            .Should()
            .BeTrue();

        CrossPageRefreshCoordinationPolicies.ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode.ImmediateDirect)
            .Should()
            .BeFalse();

        CrossPageRefreshCoordinationPolicies.ShouldArmOnInteractiveSwitch(CrossPageInteractiveSwitchRefreshMode.ImmediateScheduled)
            .Should()
            .BeFalse();
    }

    [Theory]
    [InlineData(true, true, true)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    public void ShouldRunOnPointerUp_ShouldMatchExpected(
        bool deferredFlag,
        bool crossPageDisplayActive,
        bool expected)
    {
        var shouldRun = CrossPageRefreshCoordinationPolicies.ShouldRunOnPointerUp(
            deferredFlag,
            crossPageDisplayActive);

        shouldRun.Should().Be(expected);
    }
}

public sealed class CrossPageMissingNeighborRefreshPolicyTests
{
    [Fact]
    public void Resolve_ShouldSchedule_WhenEligibleAndOutsideThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            missingCount: 2,
            lastScheduledUtc: now.AddMilliseconds(-200),
            nowUtc: now,
            minIntervalMs: 140,
            delayMs: 120);

        decision.ShouldSchedule.Should().BeTrue();
        decision.LastScheduledUtc.Should().Be(now);
        decision.DelayMs.Should().Be(120);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenWithinThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var last = now.AddMilliseconds(-20);
        var decision = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            missingCount: 1,
            lastScheduledUtc: last,
            nowUtc: now,
            minIntervalMs: 140,
            delayMs: 120);

        decision.ShouldSchedule.Should().BeFalse();
        decision.LastScheduledUtc.Should().Be(last);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenInactiveOrNoMissingPages()
    {
        var now = DateTime.UtcNow;
        var inactive = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: false,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            missingCount: 1,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now);
        var noMissing = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            missingCount: 0,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now);

        inactive.ShouldSchedule.Should().BeFalse();
        noMissing.ShouldSchedule.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldScheduleLowFrequency_WhenInteractionIsActiveAndMissingIsHigh()
    {
        var now = DateTime.UtcNow;
        var decision = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: true,
            missingCount: 3,
            lastScheduledUtc: now.AddMilliseconds(-500),
            nowUtc: now);

        decision.ShouldSchedule.Should().BeTrue();
        decision.DelayMs.Should().Be(220);
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenInteractionIsActiveAndMissingIsLow()
    {
        var now = DateTime.UtcNow;
        var decision = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: true,
            missingCount: 1,
            lastScheduledUtc: now.AddMilliseconds(-500),
            nowUtc: now);

        decision.ShouldSchedule.Should().BeFalse();
    }

    [Fact]
    public void Resolve_ShouldSkip_WhenInteractionIsActiveButWithinInteractionThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var last = now.AddMilliseconds(-200);
        var decision = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: true,
            missingCount: 3,
            lastScheduledUtc: last,
            nowUtc: now);

        decision.ShouldSchedule.Should().BeFalse();
        decision.LastScheduledUtc.Should().Be(last);
        decision.DelayMs.Should().Be(220);
    }

    [Fact]
    public void Resolve_ShouldNormalizeInvalidThresholdParameters()
    {
        var now = DateTime.UtcNow;
        var decision = CrossPageRefreshCoordinationPolicies.Resolve(
            photoModeActive: true,
            crossPageDisplayEnabled: true,
            interactionActive: true,
            missingCount: 1,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now,
            minIntervalMs: 0,
            delayMs: 0,
            interactionMinIntervalMs: -10,
            interactionDelayMs: -20,
            interactionMissingThreshold: 0);

        decision.ShouldSchedule.Should().BeTrue();
        decision.DelayMs.Should().Be(CrossPageMissingNeighborRefreshNormalizationDefaults.MinPositiveIntervalMs);
    }
}

public sealed class CrossPageNavigationCurrentInkRefreshPolicyTests
{
    [Theory]
    [InlineData(true, true, true, (int)PaintToolMode.Brush)]
    [InlineData(true, true, true, (int)PaintToolMode.Eraser)]
    public void ShouldRequest_ShouldReturnTrue_ForInteractivePageSwitchWithInkMutationTools(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var shouldRequest = CrossPageRefreshCoordinationPolicies.ShouldRequest(
            pageChanged,
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        shouldRequest.Should().BeTrue();
    }

    [Theory]
    [InlineData(true, false, true, (int)PaintToolMode.Brush)]
    [InlineData(true, false, true, (int)PaintToolMode.Eraser)]
    [InlineData(true, false, true, (int)PaintToolMode.RegionErase)]
    public void ShouldRequest_ShouldReturnTrue_ForStablePageSwitchWithInkMutationTools(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var shouldRequest = CrossPageRefreshCoordinationPolicies.ShouldRequest(
            pageChanged,
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        shouldRequest.Should().BeTrue();
    }

    [Theory]
    [InlineData(false, false, true, (int)PaintToolMode.Brush)]
    [InlineData(false, true, true, (int)PaintToolMode.Brush)]
    [InlineData(true, false, false, (int)PaintToolMode.Brush)]
    [InlineData(true, false, true, (int)PaintToolMode.Cursor)]
    [InlineData(true, false, true, (int)PaintToolMode.Shape)]
    [InlineData(true, true, true, (int)PaintToolMode.RegionErase)]
    public void ShouldRequest_ShouldReturnFalse_WhenContextDoesNotRequireRefresh(
        bool pageChanged,
        bool interactiveSwitch,
        bool photoInkModeActive,
        int modeValue)
    {
        var shouldRequest = CrossPageRefreshCoordinationPolicies.ShouldRequest(
            pageChanged,
            interactiveSwitch,
            photoInkModeActive,
            (PaintToolMode)modeValue);

        shouldRequest.Should().BeFalse();
    }
}

public sealed class CrossPagePostInputDelayPolicyTests
{
    [Fact]
    public void ResolveMs_ShouldUseConfiguredDelay_ForPostInput()
    {
        var value = CrossPageRefreshCoordinationPolicies.ResolveMs(
            CrossPageUpdateSources.PostInput,
            configuredDelayMs: 120);

        value.Should().Be(120);
    }

    [Fact]
    public void ResolveMs_ShouldRaiseDelay_ForNeighborRender()
    {
        var value = CrossPageRefreshCoordinationPolicies.ResolveMs(
            CrossPageUpdateSources.NeighborRender,
            configuredDelayMs: 120);

        value.Should().Be(CrossPagePostInputDelayThresholds.NeighborRenderMinMs);
    }

    [Fact]
    public void ResolveMs_ShouldRaiseDelay_ForReplaySource()
    {
        var value = CrossPageRefreshCoordinationPolicies.ResolveMs(
            CrossPageUpdateSources.InkVisualSyncReplay,
            configuredDelayMs: 120);

        value.Should().Be(CrossPagePostInputDelayThresholds.ReplayMinMs);
    }

    [Fact]
    public void ResolveMs_ShouldHonorOverrideBeforeMinimumRules()
    {
        var value = CrossPageRefreshCoordinationPolicies.ResolveMs(
            CrossPageUpdateSources.PostInput,
            configuredDelayMs: 120,
            fallbackDelayMs: CrossPagePostInputDelayThresholds.FallbackDelayMs,
            delayOverrideMs: 260);

        value.Should().Be(260);
    }
}

public sealed class CrossPagePostInputRefreshDelayClampPolicyTests
{
    [Fact]
    public void Clamp_ShouldRespectLowerBound()
    {
        var value = CrossPageRefreshCoordinationPolicies.Clamp(0);

        value.Should().Be(CrossPageRefreshCoordinationPolicies.MinDelayMs);
    }

    [Fact]
    public void Clamp_ShouldRespectUpperBound()
    {
        var value = CrossPageRefreshCoordinationPolicies.Clamp(1000);

        value.Should().Be(CrossPageRefreshCoordinationPolicies.MaxDelayMs);
    }

    [Fact]
    public void Clamp_ShouldKeepValue_WhenWithinBounds()
    {
        var value = CrossPageRefreshCoordinationPolicies.Clamp(220);

        value.Should().Be(220);
    }
}
