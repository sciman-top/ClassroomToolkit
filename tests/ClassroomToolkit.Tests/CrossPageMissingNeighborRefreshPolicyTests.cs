using System;
using ClassroomToolkit.App.Paint;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests;

public sealed class CrossPageMissingNeighborRefreshPolicyTests
{
    [Fact]
    public void Resolve_ShouldSchedule_WhenEligibleAndOutsideThrottleWindow()
    {
        var now = DateTime.UtcNow;
        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
        var inactive = CrossPageMissingNeighborRefreshPolicy.Resolve(
            photoModeActive: false,
            crossPageDisplayEnabled: true,
            interactionActive: false,
            missingCount: 1,
            lastScheduledUtc: CrossPageRuntimeDefaults.UnsetTimestampUtc,
            nowUtc: now);
        var noMissing = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
        var decision = CrossPageMissingNeighborRefreshPolicy.Resolve(
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
