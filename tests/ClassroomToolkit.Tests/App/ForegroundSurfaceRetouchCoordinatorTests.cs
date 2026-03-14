using System;
using ClassroomToolkit.App.Windowing;
using FluentAssertions;
using Xunit;

namespace ClassroomToolkit.Tests.App;

public sealed class ForegroundSurfaceRetouchCoordinatorTests
{
    [Fact]
    public void ApplyOverlayActivated_ShouldReturnSuppressed_WhenSuppressionWasConsumed()
    {
        var markCount = 0;
        var applyCount = 0;

        var result = ForegroundSurfaceRetouchCoordinator.ApplyOverlayActivated(
            suppressionConsumed: true,
            decision: new SurfaceZOrderDecision(
                ShouldTouchSurface: false,
                Surface: ZOrderSurface.None,
                RequestZOrderApply: false,
                ForceEnforceZOrder: false),
            lastRetouchUtc: DateTime.UnixEpoch,
            nowUtc: DateTime.UnixEpoch.AddSeconds(10),
            minimumIntervalMs: 50,
            _ => markCount++,
            _ => applyCount++);

        result.Applied.Should().BeFalse();
        result.SuppressionConsumed.Should().BeTrue();
        markCount.Should().Be(0);
        applyCount.Should().Be(0);
    }

    [Fact]
    public void ApplyOverlayActivated_ShouldMarkAndApply_WhenRetouchIsAllowed()
    {
        var markedAt = DateTime.MinValue;
        var applyCount = 0;
        var nowUtc = DateTime.UnixEpoch.AddSeconds(10);

        var result = ForegroundSurfaceRetouchCoordinator.ApplyOverlayActivated(
            suppressionConsumed: false,
            decision: new SurfaceZOrderDecision(
                ShouldTouchSurface: false,
                Surface: ZOrderSurface.None,
                RequestZOrderApply: true,
                ForceEnforceZOrder: false),
            lastRetouchUtc: DateTime.UnixEpoch,
            nowUtc: nowUtc,
            minimumIntervalMs: 50,
            value => markedAt = value,
            _ => applyCount++);

        result.Applied.Should().BeTrue();
        result.Reason.Should().Be(OverlayActivationRetouchReason.None);
        markedAt.Should().Be(nowUtc);
        applyCount.Should().Be(1);
    }

    [Fact]
    public void ApplyExplicitForeground_ShouldSkip_WhenThrottled()
    {
        var state = ExplicitForegroundRetouchRuntimeState.Default with
        {
            LastRetouchUtc = DateTime.UnixEpoch.AddSeconds(10)
        };
        var markCount = 0;
        var applyCount = 0;

        var result = ForegroundSurfaceRetouchCoordinator.ApplyExplicitForeground(
            state,
            nowUtc: DateTime.UnixEpoch.AddSeconds(10).AddMilliseconds(10),
            minimumIntervalMs: 50,
            decision: new SurfaceZOrderDecision(
                ShouldTouchSurface: true,
                Surface: ZOrderSurface.PhotoFullscreen,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true),
            _ => markCount++,
            _ => applyCount++);

        result.Applied.Should().BeFalse();
        result.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.Throttled);
        markCount.Should().Be(0);
        applyCount.Should().Be(0);
    }

    [Fact]
    public void ApplyExplicitForeground_ShouldMarkAndApply_WhenAllowed()
    {
        var markedAt = DateTime.MinValue;
        var applyCount = 0;
        var nowUtc = DateTime.UnixEpoch.AddSeconds(10);

        var result = ForegroundSurfaceRetouchCoordinator.ApplyExplicitForeground(
            ExplicitForegroundRetouchRuntimeState.Default,
            nowUtc: nowUtc,
            minimumIntervalMs: 50,
            decision: new SurfaceZOrderDecision(
                ShouldTouchSurface: true,
                Surface: ZOrderSurface.PhotoFullscreen,
                RequestZOrderApply: true,
                ForceEnforceZOrder: true),
            value => markedAt = value,
            _ => applyCount++);

        result.Applied.Should().BeTrue();
        result.Reason.Should().Be(ForegroundExplicitRetouchThrottleReason.None);
        markedAt.Should().Be(nowUtc);
        applyCount.Should().Be(1);
    }
}
